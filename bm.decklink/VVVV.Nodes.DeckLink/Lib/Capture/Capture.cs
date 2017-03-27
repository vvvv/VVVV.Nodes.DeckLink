﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DeckLinkAPI;
using System.Threading;
using System.Runtime.InteropServices;
using FeralTic.DX11.Resources;
using System.Threading.Tasks;
using FeralTic.DX11;
using VVVV.Utils.VMath;
using System.Diagnostics;
using VVVV.DeckLink.Presenters;
using VVVV.DeckLink.Utils;

namespace VVVV.DeckLink
{
    public class DecklinkCaptureThread : IDisposable, IDeckLinkInputCallback
	{
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, int length);

        private CaptureDeviceInformation deviceInfo;

        private IDeckLinkInput device;
        private _BMDVideoInputFlags videoInputFlags;
        private _BMDPixelFormat inputPixelFormat = _BMDPixelFormat.bmdFormat8BitYUV;
        private DecklinkVideoFrameConverter videoConverter;

        private IDecklinkFramePresenter framePresenter;

        private bool running = false;
        private object syncRoot = new object();
        private int slicedInput;

        private Stopwatch sw = Stopwatch.StartNew();
        private Stopwatch textureUpdateWatch = Stopwatch.StartNew();

        public int Width { get; private set; }
		public int Height { get; private set; }

        public CaptureDeviceInformation DeviceInformation
        {
            get { return this.deviceInfo; }
        }

        public bool IsRunning
        {
            get { return this.running; }
        }

        public _BMDDisplayModeSupport ModeSupport
        {
            get; private set;
        }

        public _BMDDisplayMode CurrentDisplayMode
        {
            get; private set;
        }

        public IDecklinkFramePresenter FramePresenter
        {
            get { return this.framePresenter; }
        }

        public bool AutoDetectFormatEnabled
        {
            get; private set;
        }

        public bool PerformConversion
        {
            get; private set;
        }

        public double FrameDelayTime;
        public double FrameTextureTime;

        public event EventHandler RawFrameReceived;
        public event EventHandler FrameAvailable;

		public DecklinkCaptureThread()
		{
			Width = 0;
			Height = 0;
            running = false;
		}

        public DecklinkCaptureThread(int deviceIndex, FrameQueueMode queueMode,
            int presentationCount, _BMDVideoInputFlags videoInputFlags, bool performConversion, int poolSize,
            int maxQueueSize)
        {
            DeviceFactory df=null;
            this.PerformConversion = performConversion;

            TaskUtils.RunSync(() =>
            {
                df = new DeviceFactory(deviceIndex);
                if (df.DeviceInformation.IsValid)
                {
                    this.videoConverter = new DecklinkVideoFrameConverter(df.DeckLinkDevice);
                }
            });

            this.deviceInfo = df.DeviceInformation;

            if (df.DeviceInformation.IsValid)
            {
                this.device = df.InputDevice;
                this.videoInputFlags = videoInputFlags;
                this.AutoDetectFormatEnabled = videoInputFlags == _BMDVideoInputFlags.bmdVideoInputEnableFormatDetection;
                this.slicedInput = df.slicedInput;
            }
            
            if (queueMode == FrameQueueMode.Discard)
            {
                this.framePresenter = new DiscardFramePresenter(this.videoConverter);
            }
            else if (queueMode == FrameQueueMode.Queued)
            {
                this.framePresenter = new QueuedFramePresenter(this.videoConverter, presentationCount, poolSize,maxQueueSize);
            }
            else
            {
                this.framePresenter = new TimeQueuedFramePresenter(this.videoConverter, presentationCount, poolSize, maxQueueSize);
            }
        }

        public void StartCapture(_BMDDisplayMode initialDisplayMode)
        {
            if (this.running)
                return;

            Task t = Task.Run(() =>
            {
                _BMDDisplayModeSupport displayModeSupported;
                IDeckLinkDisplayMode outputDisplayMode;
                this.device.DoesSupportVideoMode(initialDisplayMode, this.inputPixelFormat, this.videoInputFlags, out displayModeSupported, out outputDisplayMode);
                this.ModeSupport = displayModeSupported;
                
                if (displayModeSupported != _BMDDisplayModeSupport.bmdDisplayModeNotSupported)
                {
                    this.device.SetCallback(this);
                    this.ApplyDisplayMode(initialDisplayMode, this.videoInputFlags);
                    this.CurrentDisplayMode = outputDisplayMode.GetDisplayMode();
                    this.device.StartStreams();
                    this.running = true;
                }
            });
            Task.WaitAll(t);
        }

        public void SetDisplayMode(_BMDDisplayMode displayMode, _BMDVideoInputFlags videoInputFlags)
        {
            lock(syncRoot)
            {
                if (this.running)
                {
                    this.device.DisableVideoInput();
                    this.device.StopStreams();
                    this.ApplyDisplayMode(displayMode, this.videoInputFlags);
                    this.device.StartStreams();
                }
            }

        }

        private void ApplyDisplayMode(_BMDDisplayMode newDisplayMode, _BMDVideoInputFlags videoInputFlags)
        {
            _BMDDisplayModeSupport displayModeSupported;
            IDeckLinkDisplayMode outputDisplayMode;

            this.device.DoesSupportVideoMode(newDisplayMode, this.inputPixelFormat, videoInputFlags, out displayModeSupported, out outputDisplayMode);
            this.ModeSupport = displayModeSupported;
            this.CurrentDisplayMode = outputDisplayMode.GetDisplayMode();


            if (displayModeSupported != _BMDDisplayModeSupport.bmdDisplayModeNotSupported)
            {
                this.device.EnableVideoInput(newDisplayMode, this.inputPixelFormat, this.videoInputFlags);
            }
        }

        public void StopCapture()
        {
            if (!this.running)
                return;

            TaskUtils.RunSyncAndIgnoreException(() =>
            {
                this.device.SetCallback(null);
                this.device.StopStreams();
            });

            this.running = false;
        }
    
        public void VideoInputFormatChanged(_BMDVideoInputFormatChangedEvents notificationEvents, IDeckLinkDisplayMode newDisplayMode, _BMDDetectedVideoInputFormatFlags detectedSignalFlags)
        {
            lock (syncRoot)
            {
                this.device.PauseStreams();
                this.ApplyDisplayMode(newDisplayMode.GetDisplayMode(), this.videoInputFlags);
                this.device.FlushStreams();
                this.device.StartStreams();
            }
        }

        private bool isLastFrameConverted;

        public void VideoInputFrameArrived(IDeckLinkVideoInputFrame videoFrame, IDeckLinkAudioInputPacket audioPacket)
        {
            this.sw.Stop();
            this.FrameDelayTime = this.sw.Elapsed.TotalMilliseconds;
            this.sw.Restart();
            if (this.RawFrameReceived != null)
            {
                this.RawFrameReceived(this, new EventArgs());
            }

            lock (syncRoot)
            {
                this.Width = videoFrame.GetWidth();
                this.Height = videoFrame.GetHeight();

                this.framePresenter.PushFrame(videoFrame, this.PerformConversion);

                System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);
                this.isLastFrameConverted = this.PerformConversion;
            }

            if (this.FrameAvailable != null)
            {
                this.FrameAvailable(this, new EventArgs());
            }
        }

        public void AcquireTexture(DX11RenderContext context, ref DX11DynamicTexture2D texture)
        {
            lock (syncRoot)
            {
                int w = this.isLastFrameConverted ? this.Width : this.Width / 2;
                if (texture != null)
                {
                    if (texture.Width != w || texture.Height != this.Height)
                    {
                        texture.Dispose();
                        texture = null;
                    }
                }

                if (texture == null)
                {
                    var fmt = this.PerformConversion ? SlimDX.DXGI.Format.B8G8R8A8_UNorm : SlimDX.DXGI.Format.R8G8B8A8_UNorm;
                    texture = new DX11DynamicTexture2D(context, Math.Max(w, 1), Math.Max(this.Height, 1), fmt);
                }
            }

        }

        public unsafe FrameDataResult Copy(DX11DynamicTexture2D tex)
        {
            this.textureUpdateWatch.Stop();
            this.FrameTextureTime = this.textureUpdateWatch.Elapsed.TotalMilliseconds;
            this.textureUpdateWatch.Restart();
            lock (syncRoot)
            {
                int w = this.isLastFrameConverted ? this.Width : this.Width / 2;

                //Get last frame
                var result = this.framePresenter.GetPresentationFrame();

                if (result.IsNew && result.CurrentFrame != null)
                {
                    if (this.isLastFrameConverted)
                    {
                        result.CurrentFrame.ConvertedFrameData.MapAndCopyFrame(tex);
                    }
                    else
                    {
                        result.CurrentFrame.RawFrameData.MapAndCopyFrame(tex);
                    }
                }
                return result;
            }
        }

        public void Dispose()
        {
            this.StopCapture();

            TaskUtils.RunSync(() =>
            {
                DeviceFactory.ReleaseDevice(slicedInput);
                int refCount = Marshal.ReleaseComObject(this.device);
            });

            if( this.framePresenter is IDisposable)
            {
                ((IDisposable)this.framePresenter).Dispose();
            }
        }
    }
}
