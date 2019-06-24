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

        public _BMDDisplayModeSupport_v10_11 ModeSupport
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

        public bool PerformConversion { get; private set; }

        public float FPS { get; private set; }

        public double FrameDelayTime;
        public double FrameTextureTime;
        public double FrameProcessTime;

        public event EventHandler RawFrameReceived;
        public event EventHandler FrameAvailable;

        private bool isLastFrameConverted;
        public int fakeDelay = 0;
        private Stopwatch processWatch = Stopwatch.StartNew();

        public DecklinkCaptureThread(int deviceIndex, DX11RenderContext renderDevice, CaptureParameters captureParameters)
        {
            DeviceFactory df = null;
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
                this.videoInputFlags = captureParameters.AutoDetect
                    ? _BMDVideoInputFlags.bmdVideoInputEnableFormatDetection
                    : _BMDVideoInputFlags.bmdVideoInputFlagDefault;
                this.AutoDetectFormatEnabled = videoInputFlags == _BMDVideoInputFlags.bmdVideoInputEnableFormatDetection;
                this.slicedInput = df.slicedInput;
                switch (captureParameters.FrameQueueMode)
                {
                    case FrameQueueMode.Discard_DEPRECATED:
                        this.framePresenter = new DiscardFramePresenter(this.videoConverter);
                        break;
                    case FrameQueueMode.Queued_DEPRECATED:
                        this.framePresenter = new QueuedFramePresenter(this.videoConverter,
                                                                       captureParameters.PresentationCount,
                                                                       captureParameters.FrameQueuePoolSize,
                                                                       captureParameters.FrameQueueMaxSize);
                        break;
                    case FrameQueueMode.Timed_DEPRECATED:
                        this.framePresenter = new TimeQueuedFramePresenter(this.videoConverter,
                                                                           captureParameters.PresentationCount,
                                                                           captureParameters.FrameQueuePoolSize,
                                                                           captureParameters.FrameQueueMaxSize);
                        break;
                    case FrameQueueMode.DiscardImmutable:
                        this.framePresenter = new DiscardImmutableFramePresenter(renderDevice,
                                                                                 this.videoConverter,
                                                                                 captureParameters.FrameQueueMaxSize);
                        break;
                    case FrameQueueMode.Wait:
                        //IDeckLinkDisplayMode currentDisplaymode;
                        //this.device.GetDisplayMode(this.CurrentDisplayMode, out currentDisplaymode);
                        //long timeScale, frameDuration;
                        //currentDisplaymode.GetFrameRate(out frameDuration, out timeScale);
                        //var fps = timeScale / (float)frameDuration;
                        this.framePresenter = new WaitFramePresenter(this.videoConverter);
                        break;
                    default:
                        this.framePresenter = new TimeQueuedImmutableFramePresenter(renderDevice,
                                                                                    this.videoConverter,
                                                                                    captureParameters.PresentationCount,
                                                                                    captureParameters.FrameQueuePoolSize,
                                                                                    captureParameters.FrameQueueMaxSize);
                        break;
                }

            }
        }

        private bool IsDisplayModeSupported(_BMDDisplayMode displayMode)
        {
            int supported;
            _BMDVideoConnection connection = _BMDVideoConnection.bmdVideoConnectionSDI;
            _BMDSupportedVideoModeFlags flags = _BMDSupportedVideoModeFlags.bmdSupportedVideoModeDefault;
            this.device.DoesSupportVideoMode(connection, displayMode, this.inputPixelFormat, flags, out supported);
            this.CurrentDisplayMode = displayMode;
            return Convert.ToBoolean(supported);
        }

        public void StartCapture(_BMDDisplayMode initialDisplayMode)
        {
            if (this.running) return;
            Task t = Task.Run(() =>
            {
                bool isSupported = IsDisplayModeSupported(initialDisplayMode);
                // The requested mode is supported
                if (isSupported)
                {
                    ModeSupport = _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupported_v10_11;
                    this.device.SetCallback(this);
                    this.ApplyDisplayMode(initialDisplayMode);
                    this.CurrentDisplayMode = initialDisplayMode;
                    this.device.StartStreams();
                    this.running = true;
                }
                // The requested mode is not supported
                else
                {
                    // Try to get a fallback display mode
                    IDeckLinkDisplayMode fallbackModeOut;
                    this.device.GetDisplayMode(initialDisplayMode, out fallbackModeOut);
                    _BMDDisplayMode fallBackDisplayMode = fallbackModeOut.GetDisplayMode();
                    bool isFallbackDisplayModeSupported = IsDisplayModeSupported(fallBackDisplayMode);
                    // Found a fallback display mode
                    if (isFallbackDisplayModeSupported)
                    {
                        ModeSupport = _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupportedWithConversion_v10_11;
                        this.device.SetCallback(this);
                        this.ApplyDisplayMode(fallBackDisplayMode);
                        this.CurrentDisplayMode = fallBackDisplayMode;
                        this.device.StartStreams();
                        this.running = true;
                    }
                    // Theres no fallback mode
                    else
                    {
                        ModeSupport = _BMDDisplayModeSupport_v10_11.bmdDisplayModeNotSupported_v10_11;
                    }
                }
            });
            Task.WaitAll(t);
        }

        public void SetDisplayMode(_BMDDisplayMode displayMode, _BMDVideoInputFlags videoInputFlags)
        {
            lock (syncRoot)
            {
                if (this.running)
                {
                    this.CurrentDisplayMode = displayMode;
                    this.device.DisableVideoInput();
                    this.device.StopStreams();
                    this.ApplyDisplayMode(displayMode);
                    this.device.StartStreams();
                }
            }
        }

        private void ApplyDisplayMode(_BMDDisplayMode newDisplayMode)
        {
            bool isSupported = IsDisplayModeSupported(newDisplayMode);
            this.CurrentDisplayMode = newDisplayMode;
            if (isSupported)
            {
                this.device.EnableVideoInput(newDisplayMode, this.inputPixelFormat, this.videoInputFlags);
            }
            IDeckLinkDisplayMode currentDisplaymode;
            this.device.GetDisplayMode(newDisplayMode, out currentDisplaymode);
            long timeScale, frameDuration;
            currentDisplaymode.GetFrameRate(out frameDuration, out timeScale);
            var fps = timeScale / (float)frameDuration;
            this.FPS = fps;
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

        private int availFrameCount;
        public int AvailableFrameCount
        {
            get
            {
                return availFrameCount;
            }
        }

        public void VideoInputFormatChanged(
            _BMDVideoInputFormatChangedEvents notificationEvents,
            IDeckLinkDisplayMode newDisplayMode,
            _BMDDetectedVideoInputFormatFlags detectedSignalFlags)
        {
            lock (syncRoot)
            {
                this.device.PauseStreams();
                this.ApplyDisplayMode(newDisplayMode.GetDisplayMode());
                this.device.FlushStreams();
                this.device.StartStreams();
                this.CurrentDisplayMode = newDisplayMode.GetDisplayMode();
            }
        }

        public void VideoInputFrameArrived(IDeckLinkVideoInputFrame videoFrame, IDeckLinkAudioInputPacket audioPacket)
        {
            this.sw.Stop();
            this.FrameDelayTime = this.sw.Elapsed.TotalMilliseconds;
            this.sw.Restart();
            processWatch.Restart();
            if (this.RawFrameReceived != null)
            {
                this.RawFrameReceived(this, new EventArgs());
            }
            uint res;
            this.device.GetAvailableVideoFrameCount(out res);
            this.availFrameCount = (int)res;
            if (fakeDelay > 0)
            {
                Thread.Sleep(fakeDelay);
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
            processWatch.Stop();
            this.FrameProcessTime = processWatch.Elapsed.TotalMilliseconds;
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
            if (this.framePresenter is IDisposable)
            {
                ((IDisposable)this.framePresenter).Dispose();
            }
        }
    }
}
