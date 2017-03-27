using System;
using System.Collections.Generic;
using System.Diagnostics;

using DeckLinkAPI;

using FeralTic.DX11.Resources;
using FeralTic.DX11;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.DX11;
using VVVV.DeckLink.Direct3D11;
using VVVV.DeckLink.Presenters;

namespace VVVV.DeckLink.Nodes
{

	[PluginInfo(Name = "VideoIn", Category = "DeckLink", Version = "DX11.Texture", Author = "vux", Tags = "blackmagic, capture")]
	public class VideoInDeckLinkNode : IPluginEvaluate, IDX11ResourceHost, IDisposable
	{

		#region fields & pins

		[Input("Device")]
		protected IDiffSpread<int> deviceIndex;

        [Input("Output Mode")]
        protected ISpread<TextureOutputMode> outputMode;

        [Input("Upload Mode")]
        protected IDiffSpread<FrameQueueMode> queueMode;

        /*[Input("Copy Mode")]
        protected ISpread<TextureCopyMode> copyMode;*/

        [Input("Auto Detect Mode", DefaultValue =1)]
        protected IDiffSpread<bool> autoDetect;

        [Input("Display Mode", IsBang =true)]
        protected IDiffSpread<_BMDDisplayMode> displayMode;

        [Input("Apply Display Mode")]
        protected IDiffSpread<bool> applyDisplayMode;

        [Input("Reset Counters", IsBang =true)]
        protected ISpread<bool> resetCounters;

        [Input("Reset Device", IsBang = true)]
        protected ISpread<bool> resetDevice;

        [Input("Frame Present Count", DefaultValue =1)]
        protected IDiffSpread<int> framePresentCount;

        [Input("Frame Queue Max Size", DefaultValue = 10)]
        protected IDiffSpread<int> frameQueueMaxSize;

        [Input("Frame Queue Pool Size", DefaultValue = 10)]
        protected IDiffSpread<int> frameQueuePoolSize;

        [Input("Max Lateness (ms)", DefaultValue = 100)]
        protected IDiffSpread<double> maxLateness;

        [Input("Flush Queue", IsBang =true)]
        protected ISpread<bool> flushFrameQueue;

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

        [Output("Frames Captured Count")]
        protected ISpread<int> framesCapturedCount;

        [Output("Frames Copied Count")]
        protected ISpread<int> framesCopiedCount;

        [Output("Is Mode Supported")]
        protected ISpread<bool> isModeSupported;

        [Output("Current Mode")]
        protected ISpread<string> currentMode;

        [Output("Model Name")]
        protected ISpread<string> modelName;

        [Output("Display Name")]
        protected ISpread<string> displayName;

        [Output("Frames Queue Size")]
        protected ISpread<int> framesQueueSize;

        [Output("Current Frame Present Count")]
        protected ISpread<int> currentFramePresentCount;

        [Output("Frames Dropped Count")]
        protected ISpread<int> framesDroppedCount;

        [Output("Delay between frames")]
        protected ISpread<double> delayBetweenFrames;

        [Output("Delay between texture updates")]
        protected ISpread<double> delayBetweenTextureUpdates;

        [Output("Current Delay")]
        protected ISpread<double> currentDelay;

        [Output("Status")]
		protected ISpread<string> statusOutput;

        private DecklinkCaptureThread captureThread;
        private DX11Resource<YuvToRGBConverter> pixelShaderConverter = new DX11Resource<YuvToRGBConverter>();
        private DX11Resource<YuvToRGBConverterWithTarget> pixelShaderTargetConverter = new DX11Resource<YuvToRGBConverterWithTarget>();
        private DX11Resource<DX11DynamicTexture2D> rawTexture = new DX11Resource<DX11DynamicTexture2D>();
        #endregion fields & pins

        private void CreateCaptureThread()
        {
            this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0],
                this.queueMode[0], this.framePresentCount[0] > 0 ? this.framePresentCount[0] : 1,
                this.autoDetect[0] ? _BMDVideoInputFlags.bmdVideoInputEnableFormatDetection : _BMDVideoInputFlags.bmdVideoInputFlagDefault,
                this.outputMode[0] == TextureOutputMode.UncompressedBMD, 
                this.frameQueuePoolSize[0] > 0 ? this.frameQueuePoolSize[0] : 1,
                this.frameQueueMaxSize[0] > 0 ? this.frameQueueMaxSize[0] : 1);
            this.statusOutput[0] = this.captureThread.DeviceInformation.Message;
            
        }


        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
		{
            if (this.textureOutput[0] == null) { this.textureOutput[0] = new DX11Resource<DX11Texture2D>(); }

            textureOutput.SliceCount = 1;
            statusOutput.SliceCount = 1;
            isModeSupported.SliceCount = 1;


            bool newDevice = false;
            if (this.autoDetect.IsChanged || this.deviceIndex.IsChanged || 
                this.resetDevice[0] || this.queueMode.IsChanged ||this.frameQueuePoolSize.IsChanged
                ||  this.framePresentCount.IsChanged || this.outputMode.IsChanged)
            {
                if (this.captureThread != null)
                {
                    this.captureThread.Dispose();
                    this.captureThread = null;
                }

                this.CreateCaptureThread();


                if (this.captureThread.DeviceInformation.IsValid)
                {
                    this.captureThread.FrameAvailable += this.cap_NewFrame;
                }
                else
                {
                    this.captureThread = null;
                }
                this.framesCapturedCount[0] = 0;
                this.framesCopiedCount[0] = 0;
                this.framesDroppedCount[0] = 0;
                newDevice = true;
            }

            if (this.captureThread != null)
            {
                if (!this.autoDetect[0])
                {
                    //To reset display mode, we need to reinit
                    if (this.applyDisplayMode[0])
                    {
                        if (this.displayMode[0] != this.captureThread.CurrentDisplayMode)
                        {
                            this.captureThread.FrameAvailable -= this.cap_NewFrame;
                            this.captureThread.Dispose();
                        }

                        this.CreateCaptureThread();
                        
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
                        this.captureThread.StartCapture(this.displayMode[0]);
                    }
                    else
                    {
                        this.captureThread.StopCapture();
                    }
                }


            }

            if (this.resetCounters.SliceCount > 0 && resetCounters[0])
            {
                this.framesCapturedCount[0] = 0;
                this.framesCopiedCount[0] = 0;

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
                if (this.captureThread.FramePresenter is IDiscardCounter)
                {
                    this.framesDroppedCount[0] = ((IDiscardCounter)this.captureThread.FramePresenter).DiscardCount;
                }

                if (this.captureThread.FramePresenter is TimeQueuedFramePresenter)
                {
                    ((TimeQueuedFramePresenter)this.captureThread.FramePresenter).MaxFrameLateness = this.maxLateness[0];
                    this.currentDelay[0] = ((TimeQueuedFramePresenter)this.captureThread.FramePresenter).CurrentDelay;
                }

                this.isModeSupported[0] = this.captureThread.ModeSupport != _BMDDisplayModeSupport.bmdDisplayModeNotSupported;
                this.currentMode[0] = this.captureThread.CurrentDisplayMode.ToString();
                this.width[0] = this.captureThread.Width;
                this.height[0] = this.captureThread.Height;
                this.running[0] = this.captureThread.IsRunning;
                this.modelName[0] = this.captureThread.DeviceInformation.ModelName;
                this.displayName[0] = this.captureThread.DeviceInformation.DisplayName;
                this.framesQueueSize[0] = this.captureThread.FramePresenter.QueueSize;
                this.delayBetweenFrames[0] = this.captureThread.FrameDelayTime;
                this.delayBetweenTextureUpdates[0] = this.captureThread.FrameTextureTime;
                
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
                this.delayBetweenFrames[0] =0.0;
                this.delayBetweenTextureUpdates[0] = 0.0;
            }
		}

        private void cap_NewFrame(object sender, EventArgs e)
        {
            this.framesCapturedCount[0] = this.framesCapturedCount[0] + 1;
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
            this.captureThread.AcquireTexture(context, ref inputTexture);
            this.rawTexture[context] = inputTexture;

            var result = this.captureThread.Copy(inputTexture);
            bool isNew = result.IsNew;

            this.currentFramePresentCount[0] = result.PresentationCount;

            //Perform pixel conversion if applicable
            if (this.outputMode[0] == TextureOutputMode.UncompressedPS && isNew)
            {
                DX11Texture2D converted = this.pixelShaderTargetConverter[context].Apply(inputTexture);
                this.textureOutput[0][context] = converted;
            }
            else
            {
                this.textureOutput[0][context] = inputTexture;
            }

            if (isNew)
                this.framesCopiedCount[0] = this.framesCopiedCount[0] + 1;
        }
    }
}
