﻿using System;
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
#if DEBUG
using Bugsnag;
#endif

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

        #region Inputs
        [Input("Device", DefaultValue = 1)]
        protected IDiffSpread<int> FIn_DeviceIndex;

        [Input("Capture Parameters")]
        protected ISpread<CaptureParameters> FIn_CaptureParameters;

        [Input("Apply Display Mode", IsBang = true)]
        protected IDiffSpread<bool> FIn_BangApplyDisplayMode;

        [Input("Reset Counters", IsBang = true)]
        protected Pin<bool> FIn_BangResetCounters;

        [Input("Reset Device", IsBang = true)]
        protected Pin<bool> FIn_ResetDevice;

        [Input("Flush Queue", IsBang = true)]
        protected ISpread<bool> FIn_BangFlushQueue;

        [Input("Fake Delay", Visibility = PinVisibility.OnlyInspector)]
        protected ISpread<int> FIn_FakeDelay;

        [Input("Enabled")]
        protected IDiffSpread<bool> FIn_IsDeviceEnabled;
        #endregion

        #region Outputs
        [Output("Texture Out")]
        protected ISpread<DX11Resource<DX11Texture2D>> FOut_TextureOut;

        [Output("Texture Width")]
        protected ISpread<int> FOut_TextureWidth;

        [Output("Texture Height")]
        protected ISpread<int> FOut_TextureHeight;

        [Output("Is Running")]
        protected ISpread<bool> FOut_IsRunning;

        [Output("Is Mode Supported", IsSingle = true)]
        protected ISpread<bool> FOut_IsDisplayModeSupported;

        [Output("Is Auto Detect Mode Supported", IsSingle = true)]
        protected ISpread<string> FOut_VideoInputmode;

        [Output("Available Frame Count")]
        protected ISpread<int> FOut_AvailableFrameCount;

        [Output("Current Mode")]
        protected ISpread<string> FOut_CurrentMode;

        [Output("Model Name")]
        protected ISpread<string> FOut_DeviceModelName;

        [Output("Device Name")]
        protected ISpread<string> FOut_DeviceDisplayName;

        [Output("Status")]
        protected ISpread<string> FOut_Status;

        [Output("Statistics")]
        protected ISpread<CaptureStatistics> FOut_CaptureStatistics;

        [Output("Queue Data")]
        protected ISpread<double> FOut_QueueData;

        [Output("Decklink SDK Version", Visibility = PinVisibility.OnlyInspector, IsSingle = true)]
        public ISpread<string> FOut_DeckLinkAPIVersion;

        [Output("Version", Visibility = PinVisibility.OnlyInspector, IsSingle = true)]
        public ISpread<string> FOut_Version;
        #endregion

        #region Class variables
        private bool needsInitialization = true;
        private bool newDevice = false;

        private CaptureParameters _currentCaptureParameters = CaptureParameters.Default;
        private CaptureStatistics _captureStatistics = new CaptureStatistics();
        private DecklinkCaptureThread captureThread;
        private EventWaitHandle eventWaitHandle;
        private DX11RenderContext renderDevice;
        private DX11Resource<YuvToRGBConverter> pixelShaderConverter = new DX11Resource<YuvToRGBConverter>();
        private DX11Resource<YuvToRGBConverterWithTarget> pixelShaderTargetConverter = new DX11Resource<YuvToRGBConverterWithTarget>();
        private DX11Resource<DX11DynamicTexture2D> rawTexture = new DX11Resource<DX11DynamicTexture2D>();
        #endregion

#if DEBUG
        private Bugsnag.Client bugsnag = new Bugsnag.Client(new Configuration("c5a577d93d33c483f88192f8a25f14c4"));
#endif


        #region Constructor
        public VideoInDeckLinkNode()
        {
            this.renderDevice = DX11GlobalDevice.DeviceManager.RenderContexts[0];
        }
        #endregion


        #region Main loop event handlers
        private void MainLoop_OnPrepareGraph(Object sender, EventArgs args)
        {
            if (eventWaitHandle == null)
                eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            if (this.captureThread != null && this.captureThread.FramePresenter is WaitFramePresenter)
                eventWaitHandle.WaitOne();
        }
        #endregion


        #region VVVV lifecycle
        public void OnImportsSatisfied()
        {
            // Setup version/git commit pin
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = (AssemblyInformationalVersionAttribute)assembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
            var versionString = version.InformationalVersion;
            FOut_Version[0] = versionString;
#if DEBUG
            FOut_Version[0] += " DEBUG";
#endif
            // Setup wait handle
            eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
            // Setup main loop event handlers
            FHDEHost.MainLoop.OnPrepareGraph += MainLoop_OnPrepareGraph;
            // Decklink API Version
            string apiVersionString;
            IDeckLinkAPIInformation apiInfo = new CDeckLinkAPIInformation();
            apiInfo.GetString(_BMDDeckLinkAPIInformationID.BMDDeckLinkAPIVersion, out apiVersionString);
            FOut_DeckLinkAPIVersion[0] = apiVersionString;
        }

        public void Evaluate(int SpreadMax)
        {
            try
            {
                /// Update slice counts
                FOut_TextureOut.SliceCount = 1;
                FOut_Status.SliceCount = 1;
                FOut_IsDisplayModeSupported.SliceCount = 1;
                /// Create the output texture
                if (this.FOut_TextureOut[0] == null)
                    this.FOut_TextureOut[0] = new DX11Resource<DX11Texture2D>();

                CaptureParameters newCaptureParameters = this.FIn_CaptureParameters.DefaultIfNilOrNull(0, CaptureParameters.Default);

                /// Initialization procedure
                var deviceIndexChanged = this.FIn_DeviceIndex.IsChanged;
                var captureParametersForceReset = this._currentCaptureParameters.NeedDeviceReset(newCaptureParameters);
                if (this.needsInitialization || deviceIndexChanged || captureParametersForceReset)
                {
                    if (pixelShaderConverter == null)
                        pixelShaderConverter = new DX11Resource<YuvToRGBConverter>();
                    if (pixelShaderTargetConverter == null)
                        pixelShaderTargetConverter = new DX11Resource<YuvToRGBConverterWithTarget>();
                    /// Remove current capture thread
                    if (this.captureThread != null)
                    {
                        this.captureThread.Dispose();
                        this.captureThread = null;
                    }
                    this.captureThread = new DecklinkCaptureThread(this.FIn_DeviceIndex[0],
                                                                   this.renderDevice,
                                                                   newCaptureParameters);
                    /// Check validity of device
                    if (this.captureThread.DeviceInformation.IsValid)
                    {
                        this.captureThread.FrameAvailableHandler += this.OnNewFrameReceived;
                        this.captureThread.RawFrameReceivedHandler += this.OnNewRawFrameReceived;
                    }
                    else
                    {
                        this.captureThread.Dispose();
                        this.captureThread = null;
                    }
                    if (FIn_IsDeviceEnabled[0])
                        this.captureThread.StartCapture(this._currentCaptureParameters.DisplayMode);
                    /// Sucessful initalization
                    this.needsInitialization = false;
                    this._currentCaptureParameters = newCaptureParameters;
                }
                /// Early return if capture thread is not valid
                if (this.captureThread == null)
                {
                    return;
                }
                /// captureThread is set, start capturing
                this.ReactOnInputPins();
                this.UpdateOutputPins();
                this.UpdateStatistics();
            }
            catch (Exception e)
            {
#if DEBUG
                bugsnag.Notify(e);
#else
                throw e;
#endif
            }
        }


        public void Dispose()
        {
            /// Dispose wait handle
            if (this.eventWaitHandle != null)
            {
                this.eventWaitHandle.Dispose();
                this.eventWaitHandle = null;
            }
            if (this.captureThread != null)
            {
                this.captureThread.FrameAvailableHandler -= this.OnNewFrameReceived;
                this.captureThread.FrameAvailableHandler -= this.OnNewRawFrameReceived;
                this.captureThread.Dispose();
                this.captureThread = null;
            }
            /// Dispose output textures
            for (int i = 0; i < this.FOut_TextureOut.SliceCount; i++)
            {
                if (this.FOut_TextureOut[0] != null)
                {
                    this.FOut_TextureOut[0].Dispose();
                    this.FOut_TextureOut[0] = null;
                }
            }
            /// Dispose pixel shader converter
            if (this.pixelShaderConverter != null)
            {
                this.pixelShaderConverter.Dispose();
                this.pixelShaderConverter = null;
            }
            if (this.pixelShaderTargetConverter != null)
            {
                this.pixelShaderTargetConverter.Dispose();
                this.pixelShaderTargetConverter = null;
            }
            /// Needs to be initialized again
            this.needsInitialization = true;
        }
        #endregion


        #region Evaluate helper methods
        private void UpdateOutputPins()
        {
            if (this.captureThread == null)
            {
                this.FOut_IsDisplayModeSupported[0] = false;
                this.FOut_CurrentMode[0] = _BMDDisplayMode.bmdModeUnknown.ToString();
                this.FOut_TextureWidth[0] = 0;
                this.FOut_TextureHeight[0] = 0;
                this.FOut_IsRunning[0] = false;
                this.FOut_DeviceModelName[0] = "Not Set";
                this.FOut_DeviceDisplayName[0] = "Not Set";
                this.FOut_AvailableFrameCount[0] = 0;
                return;
            }
            if (this.captureThread.FramePresenter is IStatusQueueReporter)
            {
                IStatusQueueReporter sqr = (IStatusQueueReporter)this.captureThread.FramePresenter;
                this.FOut_QueueData.AssignFrom(sqr.QueueData.Select(qd => qd.TotalMilliseconds));
            }
            this.FOut_IsDisplayModeSupported[0] = this.captureThread.IsModeSupported;
            this.FOut_CurrentMode[0] = this.captureThread.CurrentDisplayMode.ToString();
            this.FOut_TextureWidth[0] = this.captureThread.Width;
            this.FOut_TextureHeight[0] = this.captureThread.Height;
            this.FOut_IsRunning[0] = this.captureThread.IsRunning;
            this.FOut_DeviceModelName[0] = this.captureThread.DeviceInformation.ModelName;
            this.FOut_DeviceDisplayName[0] = this.captureThread.DeviceInformation.DisplayName;
            this.FOut_AvailableFrameCount[0] = this.captureThread.AvailableFrameCount;
            this.FOut_Status[0] = this.captureThread.DeviceInformation.Message;
        }

        private void UpdateStatistics()
        {
            if (captureThread == null)
            {
                this._captureStatistics.Reset();
                return;
            }
            var FramePresenter = this.captureThread.FramePresenter;
            this._captureStatistics.FramesDroppedCount = FramePresenter is DiscardFramePresenter
                ? ((IDiscardCounter)FramePresenter).DiscardCount
                : 0;
            this._captureStatistics.CurrentDelay = FramePresenter is ILatencyReporter
                ? ((ILatencyReporter)FramePresenter).CurrentDelay
                : 0;
            this._captureStatistics.FramesQueueSize = FramePresenter is IDecklinkQueuedFramePresenter
                ? ((IDecklinkQueuedFramePresenter)FramePresenter).QueueSize
                : 0;
            this._captureStatistics.DelayBetweenFrames = this.captureThread.FrameDelayTime;
            this._captureStatistics.DelayBetweenTextureUpdates = this.captureThread.FrameTextureTime;
            this._captureStatistics.DeckLinkFPS = Convert.ToInt32(this.captureThread.FPS);
            this.FOut_CaptureStatistics[0] = this._captureStatistics;
        }

        private void ReactOnInputPins()
        {
            /// If the device is enabled
            /// start capturing, otherwise stop
            if (FIn_IsDeviceEnabled.IsChanged)
            {
                var isEnabled = FIn_IsDeviceEnabled[0];
                if (isEnabled)
                    this.captureThread.StartCapture(this._currentCaptureParameters.DisplayMode);
                else
                    this.captureThread.StopCapture();
            }
            /// Apply display mode
            if (FIn_BangApplyDisplayMode.IsChanged)
            {
                var shouldApplyDisplayMode = FIn_BangApplyDisplayMode[0];
                if (this._currentCaptureParameters.DisplayMode != this.captureThread.CurrentDisplayMode)
                {
                    this.captureThread.FrameAvailableHandler -= this.OnNewFrameReceived;
                    this.captureThread.RawFrameReceivedHandler -= this.OnNewRawFrameReceived;
                    this.captureThread.Dispose();
                }
                this.captureThread.Dispose();
                this.captureThread = new DecklinkCaptureThread(this.FIn_DeviceIndex[0], this.renderDevice, this._currentCaptureParameters);
                if (this.captureThread.DeviceInformation.IsValid)
                {
                    this.captureThread.FrameAvailableHandler += this.OnNewFrameReceived;
                    this.captureThread.RawFrameReceivedHandler += this.OnNewRawFrameReceived;
                    this.captureThread.StartCapture(this._currentCaptureParameters.DisplayMode);
                }
            }
            /// Capture parameters changed
            if (this._currentCaptureParameters.DiffersFrom(FIn_CaptureParameters[0]))
                this.Dispose();
            /// Reset device
            if (FIn_ResetDevice.IsChanged)
            {
                var needsReset = this.FIn_ResetDevice[0];
                if (needsReset)
                {
                    this.Dispose();
                    return;
                }
            }
            /// Flush queue
            if (FIn_BangFlushQueue.IsChanged)
            {
                var shouldFlashQueue = FIn_BangFlushQueue[0];
                if (this.captureThread.FramePresenter != null)
                {
                    var hasFlushableFramePresenter = this.captureThread.FramePresenter is IFlushable;
                    if (shouldFlashQueue && hasFlushableFramePresenter)
                        ((IFlushable)this.captureThread.FramePresenter).Flush();
                }
            }
            /// Fake delay
            this.captureThread.FakeDelay = FIn_FakeDelay[0];
            /// Reset statistics
            if (FIn_BangResetCounters.IsChanged)
            {
                var shouldResetCounters = FIn_BangResetCounters[0];
                if (shouldResetCounters)
                    this._captureStatistics.Reset();
            }
        }
        #endregion


        #region CaptureThread event handler
        private void OnNewRawFrameReceived(object sender, EventArgs a)
        {
            this._captureStatistics.FramesCapturedCount++;
            if (this.captureThread != null &&
                this.captureThread.FramePresenter is WaitFramePresenter)
                this.eventWaitHandle.Set();
        }

        private void OnNewFrameReceived(object sender, EventArgs e)
        {
            this._captureStatistics.FramesCapturedCount++;
        }
        #endregion


        #region DX11
        public void Destroy(DX11RenderContext context, bool force)
        {
            if (force)
            {
                for (int i = 0; i < this.FOut_TextureOut.SliceCount; i++)
                {
                    if (this.FOut_TextureOut[0] != null)
                    {
                        this.FOut_TextureOut[0].Dispose(context);
                        this.FOut_TextureOut[0] = null;
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
            // Early return if catputre thread is not set
            if (this.captureThread == null)
                return;
            if (this.pixelShaderConverter == null || !this.pixelShaderConverter.Contains(context))
                this.pixelShaderConverter[context] = new YuvToRGBConverter(context);
            if (!this.pixelShaderTargetConverter.Contains(context))
                this.pixelShaderTargetConverter[context] = new YuvToRGBConverterWithTarget(context, this.pixelShaderConverter[context]);
            /// Return if device is not enabled
            if (!this.FIn_IsDeviceEnabled[0])
                return;
            /// Update input texture
            var inputTexture = this.rawTexture.Contains(context)
                ? this.rawTexture[context]
                : null;
            try
            {
                //Acquire texture and copy content
                var result = this.captureThread.AcquireTexture(context, ref inputTexture);
                //Remove old texture if not required anymore
                if (inputTexture == null)
                    this.rawTexture.Data.Remove(context);
                else
                    this.rawTexture[context] = inputTexture;
                if (this._currentCaptureParameters.OutputMode == TextureOutputMode.UncompressedPS)
                {
                    if (result.IsNew)
                    {
                        DX11Texture2D converted = this.pixelShaderTargetConverter[context].Apply(result.Texture);
                        this.FOut_TextureOut[0][context] = converted;
                    }
                }
                else
                    this.FOut_TextureOut[0][context] = result.Texture;
                // Statistics
                this._captureStatistics.CurrentFramePresentCount = result.PresentationCount;
                if (result.IsNew) this._captureStatistics.FramesCopiedCount++;
            }
            catch (InvalidOperationException e)
            {
                bugsnag.Notify(e);
            }
            catch (Exception e)
            {
                bugsnag.Notify(e);
            }
        }
        #endregion
    }
}
