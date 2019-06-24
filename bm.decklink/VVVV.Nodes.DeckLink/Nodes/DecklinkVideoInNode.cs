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

namespace VVVV.DeckLink.Nodes
{

	[PluginInfo(Name = "VideoIn", Category = "DeckLink", Version = "DX11.Texture", Author = "vux", Tags = "blackmagic, capture")]
	public class VideoInDeckLinkNode : 
        IPluginEvaluate, 
        IDX11ResourceHost,
        IDisposable,
        IPartImportsSatisfiedNotification
    {
		#region fields & pins

		[Input("Device")]
		protected IDiffSpread<int> deviceIndex;

        [Input("Capture Parameters")]
        protected ISpread<CaptureParameters> captureParameters;

        [Input("Apply Display Mode", IsBang =true)]
        protected IDiffSpread<bool> applyDisplayMode;

        [Input("Reset Counters", IsBang =true)]
        protected ISpread<bool> resetCounters;

        [Input("Reset Device", IsBang = true)]
        protected ISpread<bool> resetDevice;

        [Input("Flush Queue", IsBang =true)]
        protected ISpread<bool> flushFrameQueue;

        [Input("Reference Clock")]
        protected ISpread<double> referenceClock;

        [Input("Fake Delay", Visibility =PinVisibility.OnlyInspector)]
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

        private bool first = true;
        private CaptureParameters currentParameters = CaptureParameters.Default;
        private CaptureStatistics statistics = new CaptureStatistics();

        private DX11RenderContext renderDevice;

        private DecklinkCaptureThread captureThread;
        private DX11Resource<YuvToRGBConverter> pixelShaderConverter = new DX11Resource<YuvToRGBConverter>();
        private DX11Resource<YuvToRGBConverterWithTarget> pixelShaderTargetConverter = new DX11Resource<YuvToRGBConverterWithTarget>();
        private DX11Resource<DX11DynamicTexture2D> rawTexture = new DX11Resource<DX11DynamicTexture2D>();
        #endregion fields & pins

        public VideoInDeckLinkNode()
        {
            this.renderDevice = DX11GlobalDevice.DeviceManager.RenderContexts[0];
        }

        public void OnImportsSatisfied()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = (AssemblyInformationalVersionAttribute)assembly
                .GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));
            var versionString = version.InformationalVersion;
            FVersion[0] = versionString;
        }

        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
		{
            // Setup Outputs
            if (this.textureOutput[0] == null) {
                this.textureOutput[0] = new DX11Resource<DX11Texture2D>();
            }
            textureOutput.SliceCount = 1;
            statusOutput.SliceCount = 1;
            isModeSupported.SliceCount = 1;

            bool newDevice = false;
            CaptureParameters newParameters = this.captureParameters.DefaultIfNilOrNull(0, CaptureParameters.Default);
            if (this.first == true || 
                this.deviceIndex.IsChanged || 
                this.currentParameters.NeedDeviceReset(newParameters) || 
                this.resetDevice[0])
            {
                if (this.captureThread != null)
                {
                    this.captureThread.Dispose();
                    this.captureThread = null;
                }

                this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0],this.renderDevice, newParameters); 
                this.statusOutput[0] = this.captureThread.DeviceInformation.Message;

                if (this.captureThread.DeviceInformation.IsValid)
                {
                    this.captureThread.FrameAvailable += this.cap_NewFrame;
                }
                else
                {
                    this.captureThread = null;
                }

                this.statistics.ResetCounters();
                newDevice = true;
                first = false;
            }
            this.currentParameters = newParameters;
            if (this.captureThread != null)
            {
                if (!this.currentParameters.AutoDetect)
                {
                    //To reset display mode, we need to reinit
                    if (this.applyDisplayMode[0])
                    {
                        if (this.currentParameters.DisplayMode != this.captureThread.CurrentDisplayMode)
                        {
                            this.captureThread.FrameAvailable -= this.cap_NewFrame;
                            this.captureThread.Dispose();
                        }
                        this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0], this.renderDevice, this.currentParameters);
                        this.statusOutput[0] = this.captureThread.DeviceInformation.Message;
                        if (this.captureThread.DeviceInformation.IsValid)
                        {
                            this.captureThread.FrameAvailable += this.cap_NewFrame;
                        }
                        else
                        {
                            this.captureThread = null;
                        }
                    }
                }
                if (this.FPinEnabled.IsChanged || newDevice)
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
            }

            if (this.resetCounters.SliceCount > 0 && resetCounters[0])
            {
                this.statistics.ResetCounters();
                if (this.captureThread != null)
                {
                    if (this.captureThread.FramePresenter is IDiscardCounter)
                    {
                        ((IDiscardCounter)this.captureThread.FramePresenter).Reset();
                    }
                }
            }

            if (this.flushFrameQueue[0])
            {
                if (this.captureThread.FramePresenter is IFlushable)
                {
                    ((IFlushable)this.captureThread.FramePresenter).Flush();
                }
            }


            if (this.captureThread != null)
            {
                this.captureThread.fakeDelay = this.fakeDelay[0];
                if (this.captureThread.FramePresenter is IDiscardCounter)
                {
                    this.statistics.FramesDroppedCount = ((IDiscardCounter)this.captureThread.FramePresenter).DiscardCount;
                }
                if (this.captureThread.FramePresenter is ILatencyReporter)
                {
                    ((ILatencyReporter)this.captureThread.FramePresenter).MaxFrameLateness = this.currentParameters.MaxLateness;
                    this.statistics.CurrentDelay = ((ILatencyReporter)this.captureThread.FramePresenter).CurrentDelay;
                }
                if (this.captureThread.FramePresenter is IStatusQueueReporter)
                {
                    IStatusQueueReporter sqr = (IStatusQueueReporter)this.captureThread.FramePresenter;
                    this.queueData.AssignFrom(sqr.QueueData.Select(qd => qd.TotalMilliseconds));
                   
                }
                this.isModeSupported[0] = this.captureThread.ModeSupport != _BMDDisplayModeSupport_v10_11.bmdDisplayModeNotSupported_v10_11;
                this.currentMode[0] = this.captureThread.CurrentDisplayMode.ToString();
                this.width[0] = this.captureThread.Width;
                this.height[0] = this.captureThread.Height;
                this.running[0] = this.captureThread.IsRunning;
                this.modelName[0] = this.captureThread.DeviceInformation.ModelName;
                this.displayName[0] = this.captureThread.DeviceInformation.DisplayName;
                this.statistics.FramesQueueSize = this.captureThread.FramePresenter.QueueSize;
                this.statistics.DelayBetweenFrames = this.captureThread.FrameDelayTime;
                this.statistics.DelayBetweenTextureUpdates = this.captureThread.FrameTextureTime;
                this.statistics.FrameProcessTime = this.captureThread.FrameProcessTime;
                this.statistics.FPS = Convert.ToInt32(this.captureThread.FPS);
                this.availFrameCount[0] = this.captureThread.AvailableFrameCount;
            }
            else
            {
                this.isModeSupported[0] = false;
                this.currentMode[0] = _BMDDisplayMode.bmdModeUnknown.ToString();
                this.width[0] = 0;
                this.height[0] = 0;
                this.running[0] = false;
                this.modelName[0] = "";
                this.displayName[0] = "";
                this.statistics.FPS = 0;
                this.statistics.DelayBetweenFrames =0.0;
                this.statistics.DelayBetweenTextureUpdates = 0.0;
            }
            this.captureStatisticsOutput[0] = this.statistics;
		}

        private void cap_NewFrame(object sender, EventArgs e)
        {
            this.statistics.FramesCapturedCount++;
        }

		public void Dispose()
		{
			if (this.captureThread != null)
            {
                this.captureThread.FrameAvailable -= this.cap_NewFrame;
                this.captureThread.Dispose();
                this.captureThread = null;
            }

            for (int i = 0; i < this.textureOutput.SliceCount; i++)
            {
                if (this.textureOutput[0] != null)
                {
                    this.textureOutput[0].Dispose();
                    this.textureOutput[0] = null;
                }          
            }
            if (this.pixelShaderConverter != null)
            {
                this.pixelShaderConverter.Dispose();
            }
		}

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

            if (!this.FPinEnabled[0])
                return;

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

            //Perform pixel conversion if applicable
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

            if (result.IsNew)
                this.statistics.FramesCopiedCount++;
        }
    }
}
