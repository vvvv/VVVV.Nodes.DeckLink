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
        #region DLL imports
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        #endregion

        #region Class variables
        public static extern void CopyMemory(IntPtr destination, IntPtr source, int length);
        public double FrameDelayTime;
        public double FrameTextureTime;
        public double FrameProcessTime;
        public List<_BMDDisplayMode> AvailableDisplayModes = new List<_BMDDisplayMode>();

        public event EventHandler RawFrameReceivedHandler;
        public event EventHandler FrameAvailableHandler;

        private int fakeDelay = 0;
        private CaptureDeviceInformation deviceInfo;
        private IDeckLinkInput device;
        private _BMDVideoInputFlags videoInputFlags;
        private _BMDPixelFormat inputPixelFormat = _BMDPixelFormat.bmdFormat8BitYUV;
        private DecklinkVideoFrameConverter videoConverter;
        private readonly IDecklinkFramePresenter framePresenter;
        private bool running = false;
        private object syncRoot = new object();
        private readonly int slicedInput;
        private Stopwatch sw = Stopwatch.StartNew();
        private readonly Stopwatch textureUpdateWatch = Stopwatch.StartNew();
        private bool isLastFrameConverted;
        private Stopwatch processWatch = Stopwatch.StartNew();
        private _BMDDisplayModeSupport_v10_11 ModeSupport;
        private int availFrameCount;
        bool displayModesFetched = false;
        #endregion

        #region Propertis
        public int Width { get; private set; }
        public int Height { get; private set; }

        public _BMDPixelFormat PixelFormat { get; private set; }

        public int FakeDelay { get; set; }

        public CaptureDeviceInformation DeviceInformation
        {
            get { return this.deviceInfo; }
        }

        public bool IsRunning
        {
            get { return this.running; }
        }

        public bool IsModeSupported
        {
            get; private set;
        }

        public string ModeSupportMessage
        {
            get
            {
                switch (this.ModeSupport)
                {
                    case _BMDDisplayModeSupport_v10_11.bmdDisplayModeNotSupported_v10_11:
                        return "Mode not supported";
                    case _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupportedWithConversion_v10_11:
                        return "Mode supported (with Conversion)";
                    case _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupported_v10_11:
                        return "Mode supported";
                    default:
                        return "No Information";
                }
            }
        }

        public _BMDDisplayMode CurrentDisplayMode { get; private set; }

        public IDecklinkFramePresenter FramePresenter
        {
            get { return this.framePresenter; }
        }

        public bool AutoDetectFormatEnabled { get; private set; }

        public bool ShouldPerformConversion { get; private set; }

        public float FPS { get; private set; }

        public int AvailableFrameCount
        {
            get
            {
                return availFrameCount;
            }
        }
        #endregion



        public DecklinkCaptureThread(int deviceIndex, DX11RenderContext renderDevice, CaptureParameters captureParameters)
        {
            DeviceFactory df = null;
            // TODO
            this.ShouldPerformConversion = captureParameters.OutputMode == TextureOutputMode.UncompressedBMD;
            TaskUtils.RunSync(() =>
            {
                df = new DeviceFactory(deviceIndex);
                if (df.DeviceInformation.IsValid)
                    this.videoConverter = new DecklinkVideoFrameConverter(df.DeckLinkDevice);
                else
                {
                    this.deviceInfo = df.DeviceInformation;
                    return;
                }
            });
            this.deviceInfo = df.DeviceInformation;
            if (df.DeviceInformation.IsValid)
            {
                this.device = df.InputDevice;
                this.inputPixelFormat = captureParameters.PixelFormat == PixelColorFormat.YUV8Bit
                    ? _BMDPixelFormat.bmdFormat8BitYUV
                    : _BMDPixelFormat.bmdFormat8BitBGRA;
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
            else
                this.deviceInfo = df.DeviceInformation;
        }

        private bool IsDisplayModeSupported(_BMDDisplayMode displayMode)
        {
            _BMDSupportedVideoModeFlags flags = _BMDSupportedVideoModeFlags.bmdSupportedVideoModeDefault;
            this.device.DoesSupportVideoMode(_BMDVideoConnection.bmdVideoConnectionUnspecified, displayMode, this.inputPixelFormat, flags, out int isSupported);
            return Convert.ToBoolean(isSupported);
        }

        public void StartCapture(_BMDDisplayMode initialDisplayMode)
        {
            if (this.running) return;
            Task t = Task.Run(() =>
            {
                this.IsModeSupported = IsDisplayModeSupported(initialDisplayMode);
                // The requested mode is supported
                if (this.IsModeSupported)
                {
                    ModeSupport = _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupported_v10_11;
                    this.device.GetDisplayMode(initialDisplayMode, out IDeckLinkDisplayMode usedDisplayMode);
                    this.device.SetCallback(this);
                    this.ApplyDisplayMode(initialDisplayMode);
                    this.CurrentDisplayMode = initialDisplayMode;
                    try
                    {
                        this.device.StartStreams();
                    } 
                    catch (COMException e)
                    {
                        this.deviceInfo = this.deviceInfo.Invalid("Device seems to be occupied already");
                        this.running = false;
                        throw e;
                    }
                    this.deviceInfo = this.deviceInfo.Valid("Streaming");
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

        private void ApplyDisplayMode(_BMDDisplayMode newDisplayMode)
        {
            try
            {
                bool isSupported = IsDisplayModeSupported(newDisplayMode);
                if (isSupported)
                {
                    if (this.CurrentDisplayMode != newDisplayMode)
                        this.ModeSupport = _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupportedWithConversion_v10_11;
                    else
                        this.ModeSupport = _BMDDisplayModeSupport_v10_11.bmdDisplayModeSupported_v10_11;
                    this.device.EnableVideoInput(newDisplayMode, this.inputPixelFormat, this.videoInputFlags);
                    this.CurrentDisplayMode = newDisplayMode;
                }
            }
            catch (COMException e)
            {
                this.deviceInfo = this.deviceInfo.Invalid(e.Message);
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
            }
        }

        public void VideoInputFrameArrived(IDeckLinkVideoInputFrame videoFrame, IDeckLinkAudioInputPacket audioPacket)
        {
            this.sw.Stop();
            this.FrameDelayTime = this.sw.Elapsed.TotalMilliseconds;
            this.sw.Restart();
            processWatch.Restart();
            if (this.RawFrameReceivedHandler != null)
            {
                this.RawFrameReceivedHandler(this, new EventArgs());
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
                this.PixelFormat = this.inputPixelFormat;
                int pixelFormatDivisor = this.inputPixelFormat == _BMDPixelFormat.bmdFormat8BitYUV ? 2 : 1;
                var pixelColorFormat = this.inputPixelFormat == _BMDPixelFormat.bmdFormat8BitBGRA 
                    ? SlimDX.DXGI.Format.B8G8R8A8_UNorm 
                    : SlimDX.DXGI.Format.R8G8B8A8_UNorm;
                this.framePresenter.PushFrame(videoFrame, this.ShouldPerformConversion, pixelFormatDivisor, pixelColorFormat);
                System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);
                this.isLastFrameConverted = this.ShouldPerformConversion;
            }
            if (this.FrameAvailableHandler != null)
            {
                this.FrameAvailableHandler(this, new EventArgs());
            }
            processWatch.Stop();
            this.FrameProcessTime = processWatch.Elapsed.TotalMilliseconds;
        }

        public FrameDataResult AcquireTexture(DX11RenderContext context, ref DX11DynamicTexture2D texture)
        {
            if (this.framePresenter == null)
                throw new InvalidOperationException("Frame Presenter is not set.");
            this.textureUpdateWatch.Stop();
            this.FrameTextureTime = this.textureUpdateWatch.Elapsed.TotalMilliseconds;
            this.textureUpdateWatch.Restart();
            var result = this.framePresenter.GetPresentationFrame();
            //Raw image, update and copy
            if (result.ResultType == FrameDataResultType.RawImage)
            {
                this.UpdateTexture(context, ref texture);
                if (result.IsNew && result.CurrentFrame != null)
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
            // Never happens, but for clarity set result type in condition
            else
            {
                throw new InvalidOperationException("Result type should have been either texture or raw image");
            }
        }

        private void UpdateTexture(DX11RenderContext context, ref DX11DynamicTexture2D texture)
        {
            lock (syncRoot)
            {
                int width = this.isLastFrameConverted ? this.Width : this.Width / 2;
                if (texture != null)
                {
                    if (width != this.Width || texture.Height != this.Height)
                    {
                        texture.Dispose();
                        texture = null;
                    }
                }
                if (texture == null)
                {
                    var fmt = this.ShouldPerformConversion ? 
                        SlimDX.DXGI.Format.B8G8R8A8_UNorm : 
                        SlimDX.DXGI.Format.R8G8B8A8_UNorm;
                    texture = new DX11DynamicTexture2D(context, Math.Max(1, width), Math.Max(1, Height), fmt);
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
                if (this.device == null)
                    return;
                int refCount = Marshal.ReleaseComObject(this.device);
            });
            if (this.framePresenter is IDisposable)
            {
                ((IDisposable)this.framePresenter).Dispose();
            }
        }
    }
}
