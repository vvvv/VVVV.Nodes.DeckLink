using System;
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

        public DecklinkCaptureThread(int deviceIndex, DX11RenderContext renderDevice, CaptureParameters captureParameters)
        {
            DeviceFactory df=null;
            this.PerformConversion = captureParameters.OutputMode == TextureOutputMode.UncompressedBMD;

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
                this.videoInputFlags = captureParameters.AutoDetect ? _BMDVideoInputFlags.bmdVideoInputEnableFormatDetection : _BMDVideoInputFlags.bmdVideoInputFlagDefault;
                this.AutoDetectFormatEnabled = videoInputFlags == _BMDVideoInputFlags.bmdVideoInputEnableFormatDetection;
                this.slicedInput = df.slicedInput;

                if (captureParameters.FrameQueueMode == FrameQueueMode.Discard)
                {
                    this.framePresenter = new DiscardFramePresenter(this.videoConverter);
                }
                else if (captureParameters.FrameQueueMode == FrameQueueMode.Queued)
                {
                    this.framePresenter = new QueuedFramePresenter(this.videoConverter, captureParameters.PresentationCount, captureParameters.FrameQueuePoolSize, captureParameters.FrameQueueMaxSize);
                }
                else if (captureParameters.FrameQueueMode == FrameQueueMode.Timed)
                {
                    this.framePresenter = new TimeQueuedFramePresenter(this.videoConverter, captureParameters.PresentationCount, captureParameters.FrameQueuePoolSize, captureParameters.FrameQueueMaxSize);
                }
                else if (captureParameters.FrameQueueMode == FrameQueueMode.DiscardImmutable)
                {
                    this.framePresenter = new DiscardImmutableFramePresenter(renderDevice, this.videoConverter, captureParameters.FrameQueueMaxSize);
                }
                else
                {
                    this.framePresenter = new TimeQueuedImmutableFramePresenter(renderDevice, this.videoConverter, captureParameters.PresentationCount, captureParameters.FrameQueuePoolSize, captureParameters.FrameQueueMaxSize);
                }
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

        public FrameDataResult AcquireTexture(DX11RenderContext context, ref DX11DynamicTexture2D texture)
        {
            this.textureUpdateWatch.Stop();
            this.FrameTextureTime = this.textureUpdateWatch.Elapsed.TotalMilliseconds;
            this.textureUpdateWatch.Restart();

            var result = this.framePresenter.GetPresentationFrame();

            //Raw image, update and copy
            if (result.ResultType == FrameDataResultType.RawImage)
            {
                this.UpdateTexture(context, ref texture);

                if (result.IsNew)
                {
                    this.Copy(result.CurrentFrame, texture);
                }
                return FrameDataResult.Texture2D(texture, result.IsNew, result.PresentationCount);
            }
            else if (result.ResultType == FrameDataResultType.Texture)
            {
                //Dispose old texture and return a null
                if (texture != null)
                {
                    texture.Dispose();
                    texture = null;
                }
                return result;
            }
            else //Never happens, but for clarity set result type in condition
            {
                throw new InvalidOperationException("Result type should have been either texture or raw image");
            }
        }

        private void UpdateTexture(DX11RenderContext context, ref DX11DynamicTexture2D texture)
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

        private unsafe FrameDataResult Copy(DecklinkFrameData frameData, DX11DynamicTexture2D tex)
        {
            lock (syncRoot)
            {
                int w = this.isLastFrameConverted ? this.Width : this.Width / 2;

                //Get last frame
                var result = this.framePresenter.GetPresentationFrame();
                if (this.isLastFrameConverted)
                {
                    result.CurrentFrame.ConvertedFrameData.MapAndCopyFrame(tex);
                }
                else
                {
                    result.CurrentFrame.RawFrameData.MapAndCopyFrame(tex);
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
