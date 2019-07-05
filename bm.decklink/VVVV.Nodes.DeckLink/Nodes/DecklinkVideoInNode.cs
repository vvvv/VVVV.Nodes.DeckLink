using System;
using System.Collections.Generic;
using System.Diagnostics;
using DeckLinkAPI;
using System.Reflection;
using FeralTic.DX11.Resources;
using FeralTic.DX11;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.DX11;
using VVVV.DeckLink.Direct3D11;
using VVVV.DeckLink.Presenters;
using VVVV.DeckLink.Utils;
using VVVV.DX11.Lib.Devices;
using System.Linq;
using System.ComponentModel.Composition;
using VVVV.Core.Logging;
using System.Threading;

namespace VVVV.DeckLink.Nodes
{
    [PluginInfo(Name = "VideoIn", Category = "DeckLink", Version = "DX11.Texture", Author = "vux, Guido Schmidt", Tags = "blackmagic, capture")]
    public class VideoInDeckLinkNode :
        IPluginEvaluate,
        IDX11ResourceHost,
        IDisposable,
        IPartImportsSatisfiedNotification
    {
        #region Imports
        [Import]
        private IHDEHost FHDEHost;

        [Import()]
        private ILogger Flogger;
        #endregion 


        #region Fields & pins
        [Input("Device", DefaultValue = 1)]
        protected IDiffSpread<int> deviceIndex;

        [Input("Capture Parameters")]
        protected ISpread<CaptureParameters> captureParameters;

        [Input("Apply Display Mode", IsBang = true)]
        protected IDiffSpread<bool> applyDisplayMode;

        [Input("Reset Counters", IsBang = true)]
        protected ISpread<bool> resetCounters;

        [Input("Reset Device", IsBang = true)]
        protected ISpread<bool> resetDevice;

        [Input("Flush Queue", IsBang = true)]
        protected ISpread<bool> flushFrameQueue;

        [Input("Reference Clock")]
        protected ISpread<double> referenceClock;

        [Input("Fake Delay", Visibility = PinVisibility.OnlyInspector)]
        protected ISpread<int> fakeDelay;

        [Input("Enabled")]
        protected IDiffSpread<bool> FPinEnabled;

        [Output("Texture Out")]
        protected ISpread<DX11Resource<DX11Texture2D>> textureOutput;

        [Output("Width")]
        protected ISpread<int> width;

        [Output("Height")]
        protected ISpread<int> height;

        [Output("Is Running")]
        protected ISpread<bool> running;

        [Output("Is Mode Supported")]
        protected ISpread<bool> isModeSupported;

        [Output("Available Frame Count")]
        protected ISpread<int> availFrameCount;

        [Output("Current Mode")]
        protected ISpread<string> currentMode;

        [Output("Model Name")]
        protected ISpread<string> modelName;

        [Output("Display Name")]
        protected ISpread<string> displayName;

        [Output("Status")]
        protected ISpread<string> statusOutput;

        [Output("Statistics")]
        protected ISpread<CaptureStatistics> captureStatisticsOutput;

        [Output("Queue Data")]
        protected ISpread<double> queueData;

        [Output("Version", Visibility = PinVisibility.Hidden, IsSingle = true)]
        public ISpread<string> FVersion;

        private bool needsInitialization = true;
        private bool newDevice = false;
        private CaptureParameters currentParameters = CaptureParameters.Default;
        private CaptureStatistics statistics = new CaptureStatistics();
        private DX11RenderContext renderDevice;
        private DecklinkCaptureThread captureThread;
        private DX11Resource<YuvToRGBConverter> pixelShaderConverter = new DX11Resource<YuvToRGBConverter>();
        private DX11Resource<YuvToRGBConverterWithTarget> pixelShaderTargetConverter = new DX11Resource<YuvToRGBConverterWithTarget>();
        private DX11Resource<DX11DynamicTexture2D> rawTexture = new DX11Resource<DX11DynamicTexture2D>();
        private EventWaitHandle eventWaitHandle;
        #endregion fields & pins


        #region Constructor
        public VideoInDeckLinkNode()
        {
            this.renderDevice = DX11GlobalDevice.DeviceManager.RenderContexts[0];
        }
        #endregion


        #region Main loop event handlers
        // @TODO remove unnecessary event handlers
        private void MainLoop_OnPrepareGraph(Object sender, EventArgs args)
        {
            if (this.captureThread != null &&
                this.captureThread.FramePresenter is WaitFramePresenter)
            {
                eventWaitHandle.WaitOne();
            }
        }

        private void MainLoop_OnRender(Object sender, EventArgs args)
        {
        }

        private void MainLoop_OnPresent(Object sender, EventArgs args)
        {
        }

        private void MainLoop_OnResetCache(Object sender, EventArgs args)
        {
        }

        private void MainLoop_OnUpdateView(Object sender, EventArgs args)
        {
        }
        #endregion


        #region VVVV lifecycle
        public void OnImportsSatisfied()
        {
            // Setup version/git commit pin
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = (AssemblyInformationalVersionAttribute)assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
            var versionString = version.InformationalVersion;
            FVersion[0] = versionString;
            // Setup wait handle
            eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            // Setup main loop event handlers
            FHDEHost.MainLoop.OnPrepareGraph += MainLoop_OnPrepareGraph;
            FHDEHost.MainLoop.OnRender += MainLoop_OnRender;
            FHDEHost.MainLoop.OnPresent += MainLoop_OnPresent;
            FHDEHost.MainLoop.OnResetCache += MainLoop_OnResetCache;
            FHDEHost.MainLoop.OnUpdateView += MainLoop_OnUpdateView;
        }

        public void Evaluate(int SpreadMax)
        {
            this.SetupTexture();
            this.UpdateCaptureParameters();
            this.UpdateOutputPins();
            this.UpdateStatistics();
            this.ReactOnInputPins();
        }

        public void Dispose()
        {
            this.Reset();
        }
        #endregion


        #region Evaluate helper methods
        private void Reset()
        {
            // Clean  wait handle
            this.eventWaitHandle.Dispose();
            this.eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            // Clean captureThread
            if (this.captureThread != null)
            {
                this.captureThread.StopCapture();
                this.captureThread.FrameAvailableHandler -= this.OnNewFrameReceived;
                this.captureThread.FrameAvailableHandler -= this.OnNewRawFrameReceived;
                this.captureThread.Dispose();
                this.captureThread = null;
            }
            // Clean texture outputs
            for (int i = 0; i < this.textureOutput.SliceCount; i++)
            {
                if (this.textureOutput[0] == null) continue;
                this.textureOutput[0].Dispose();
                this.textureOutput[0] = null;
            }
            // Clean YuvToRGB converter
            if (this.pixelShaderConverter != null)
            {
                this.pixelShaderConverter.Dispose();
            }
            // Setup texture output
            if (this.textureOutput[0] == null)
            {
                this.textureOutput[0] = new DX11Resource<DX11Texture2D>();
            }
            this.needsInitialization = true;
        }

        private void SetupTexture()
        {
            // Setup texture output
            if (this.textureOutput[0] == null)
            {
                this.textureOutput[0] = new DX11Resource<DX11Texture2D>();
            }
        }

        private void ChangeDisplayMode()
        {
            // Did the display mode actually change?
            if (this.currentParameters.DisplayMode != this.captureThread.CurrentDisplayMode)
            {
                this.captureThread.FrameAvailableHandler -= this.OnNewFrameReceived;
                this.captureThread.RawFrameReceivedHandler -= this.OnNewRawFrameReceived;
                this.captureThread.Dispose();
            }
            this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0], this.renderDevice, this.currentParameters);
            if (this.captureThread.DeviceInformation.IsValid)
            {
                this.captureThread.FrameAvailableHandler += this.OnNewFrameReceived;
                this.captureThread.RawFrameReceivedHandler += this.OnNewRawFrameReceived;
            }
            else
            {
                this.captureThread = null;
            }
        }

        private void UpdateCaptureParameters()
        {
            CaptureParameters newParameters = this.captureParameters.DefaultIfNilOrNull(0, CaptureParameters.Default);
            if (this.needsInitialization == true ||
                this.deviceIndex.IsChanged ||
                this.currentParameters.NeedDeviceReset(newParameters) ||
                this.resetDevice[0])
            {
                // Setup wait handle
                this.eventWaitHandle.Dispose();
                this.eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
                // Dispose old capture thread
                if (this.captureThread != null)
                {
                    this.captureThread.FrameAvailableHandler -= this.OnNewFrameReceived;
                    this.captureThread.RawFrameReceivedHandler -= this.OnNewRawFrameReceived;
                    this.captureThread.Dispose();
                    this.captureThread = null;
                }
                // Create new capture thread and validate it
                this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0], this.renderDevice, newParameters);
                if (this.captureThread.DeviceInformation.IsValid)
                {
                    this.captureThread.FrameAvailableHandler += this.OnNewFrameReceived;
                    this.captureThread.RawFrameReceivedHandler += this.OnNewRawFrameReceived;
                    _BMDVideoInputFlags flags = _BMDVideoInputFlags.bmdVideoInputFlagDefault;
                    this.captureThread.SetDisplayMode(newParameters.DisplayMode, flags);
                }
                else
                {
                    this.captureThread = null;
                }
                // Reset statistics and set flags
                this.statistics.Reset();
                this.newDevice = true;
                this.needsInitialization = false;
            }
            this.currentParameters = newParameters;
        }

        private void UpdateOutputPins()
        {
            textureOutput.SliceCount = 1;
            statusOutput.SliceCount = 1;
            isModeSupported.SliceCount = 1;
            if (this.captureThread != null)
            {
                if (this.captureThread.FramePresenter is IStatusQueueReporter)
                {
                    IStatusQueueReporter sqr = (IStatusQueueReporter)this.captureThread.FramePresenter;
                    this.queueData.AssignFrom(sqr.QueueData.Select(qd => qd.TotalMilliseconds));
                }
                this.isModeSupported[0] = this.captureThread.isModeSupported;
                this.currentMode[0] = this.captureThread.CurrentDisplayMode.ToString();
                this.width[0] = this.captureThread.Width;
                this.height[0] = this.captureThread.Height;
                this.running[0] = this.captureThread.IsRunning;
                this.modelName[0] = this.captureThread.DeviceInformation.ModelName;
                this.displayName[0] = this.captureThread.DeviceInformation.DisplayName;
                this.availFrameCount[0] = this.captureThread.AvailableFrameCount;
                this.statusOutput[0] = this.captureThread.DeviceInformation.Message;
            }
            else
            {
                this.isModeSupported[0] = false;
                //this.currentMode[0] = _BMDDisplayMode.bmdModeUnknown.ToString();
                this.width[0] = 0;
                this.height[0] = 0;
                this.running[0] = false;
                this.modelName[0] = "No Model";
                this.displayName[0] = "No Display";
                this.availFrameCount[0] = 0;
                this.statusOutput[0] = "No Status";
            }
        }

        private void UpdateStatistics()
        {
            if (captureThread != null)
            {
                var FramePresenter = this.captureThread.FramePresenter;
                this.statistics.FramesDroppedCount = FramePresenter is DiscardFramePresenter
                    ? ((IDiscardCounter)FramePresenter).DiscardCount
                    : 0;
                this.statistics.CurrentDelay = FramePresenter is ILatencyReporter
                    ? ((ILatencyReporter)FramePresenter).CurrentDelay
                    : 0;
                this.statistics.FramesQueueSize = FramePresenter is IDecklinkQueuedFramePresenter
                    ? ((IDecklinkQueuedFramePresenter)FramePresenter).QueueSize
                    : 0;
                this.statistics.DelayBetweenFrames = this.captureThread.FrameDelayTime;
                this.statistics.DelayBetweenTextureUpdates = this.captureThread.FrameTextureTime;
                this.statistics.DeckLinkFPS = Convert.ToInt32(this.captureThread.FPS);
            }
            else
            {
                this.statistics.Reset();
            }
            this.captureStatisticsOutput[0] = this.statistics;
        }

        private void ReactOnInputPins()
        {
            // Capture thread fake delay duration from input
            if (this.captureThread != null)
            {
                this.captureThread.FakeDelay = this.fakeDelay[0];
            }
            // Reset statistics 
            if (this.resetCounters.SliceCount > 0 && 
                this.resetCounters[0])
            {
                this.statistics.Reset();
                if (this.captureThread != null)
                {
                    if (this.captureThread.FramePresenter is IDiscardCounter)
                    {
                        ((IDiscardCounter)this.captureThread.FramePresenter).Reset();
                    }
                }
            }
            // Flush frame queue
            if (this.flushFrameQueue[0])
            {
                if (this.captureThread.FramePresenter is IFlushable)
                {
                    ((IFlushable)this.captureThread.FramePresenter).Flush();
                }
            }
            // Device change or enable change
            if (this.FPinEnabled.IsChanged || 
                this.newDevice)
            {
                if (this.FPinEnabled[0])
                {
                    this.captureThread.StartCapture(this.currentParameters.DisplayMode);
                }
                else
                {
                    this.captureThread.StopCapture();
                }
            }
            // React on reset pin bangging
            if (this.resetDevice[0])
            {
                this.Reset();
            }

            // When auto detection is disabled and the 'apply display mode' is banged
            if (!this.currentParameters.AutoDetect && this.applyDisplayMode[0])
            {
                this.ChangeDisplayMode();
            }
        }
        #endregion


        #region CaptureThread event handler
        private void OnNewRawFrameReceived(object sender, EventArgs a)
        {
            this.statistics.FramesCapturedCount++;
            if (this.captureThread != null &&
                this.captureThread.FramePresenter is WaitFramePresenter)
                this.eventWaitHandle.Set();
        }

        private void OnNewFrameReceived(object sender, EventArgs e)
        {
            this.statistics.FramesCapturedCount++;
        }
        #endregion


        #region DX11
        public void Destroy(DX11RenderContext context, bool force)
        {
            if (force)
            {
                for (int i = 0; i < this.textureOutput.SliceCount; i++)
                {
                    if (this.textureOutput[0] != null)
                    {
                        this.textureOutput[0].Dispose(context);
                        this.textureOutput[0] = null;
                    }
                }
                if (this.pixelShaderConverter != null)
                {
                    this.pixelShaderConverter.Dispose(context);
                }
            }
        }

        public void Update(DX11RenderContext context)
        {
            if (this.captureThread == null)
                return;
            if (!this.pixelShaderConverter.Contains(context))
            {
                this.pixelShaderConverter[context] = new YuvToRGBConverter(context);
            }
            if (!this.pixelShaderTargetConverter.Contains(context))
            {
                this.pixelShaderTargetConverter[context] = new YuvToRGBConverterWithTarget(context, this.pixelShaderConverter[context]);
            }
            if (!this.FPinEnabled[0]) return;
            var inputTexture = this.rawTexture.Contains(context) ? this.rawTexture[context] : null;
            //Acquire texture and copy content
            var result = this.captureThread.AcquireTexture(context, ref inputTexture);
            //Remove old texture if not required anymore
            if (inputTexture == null)
            {
                this.rawTexture.Data.Remove(context);
            }
            else
            {
                this.rawTexture[context] = inputTexture;
            }
            this.statistics.CurrentFramePresentCount = result.PresentationCount;
            if (this.currentParameters.OutputMode == TextureOutputMode.UncompressedPS)
            {
                if (result.IsNew)
                {
                    DX11Texture2D converted = this.pixelShaderTargetConverter[context].Apply(result.Texture);
                    this.textureOutput[0][context] = converted;
                }
            }
            else
            {
                this.textureOutput[0][context] = result.Texture;
            }
            if (result.IsNew) this.statistics.FramesCopiedCount++;
        }
        #endregion
    }
}
