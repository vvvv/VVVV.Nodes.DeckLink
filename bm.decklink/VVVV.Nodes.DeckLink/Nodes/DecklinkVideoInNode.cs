﻿using System;
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
using VVVV.DeckLink.Utils;

namespace VVVV.DeckLink.Nodes
{

	[PluginInfo(Name = "VideoIn", Category = "DeckLink", Version = "DX11.Texture", Author = "vux", Tags = "blackmagic, capture")]
	public class VideoInDeckLinkNode : IPluginEvaluate, IDX11ResourceHost, IDisposable
	{

		#region fields & pins

		[Input("Device")]
		protected IDiffSpread<int> deviceIndex;

        [Input("Catpure Parameters")]
        protected ISpread<CaptureParameters> captureParameters;

        /*[Input("Output Mode")]
        protected ISpread<TextureOutputMode> outputMode;

        [Input("Upload Mode")]
        protected IDiffSpread<FrameQueueMode> queueMode;*/

        /*[Input("Copy Mode")]
        protected ISpread<TextureCopyMode> copyMode;*/

       /* [Input("Auto Detect Mode", DefaultValue =1)]
        protected IDiffSpread<bool> autoDetect;

        [Input("Display Mode", IsBang =true)]
        protected IDiffSpread<_BMDDisplayMode> displayMode;*/

        [Input("Apply Display Mode")]
        protected IDiffSpread<bool> applyDisplayMode;

        [Input("Reset Counters", IsBang =true)]
        protected ISpread<bool> resetCounters;

        [Input("Reset Device", IsBang = true)]
        protected ISpread<bool> resetDevice;

        /*[Input("Frame Present Count", DefaultValue =1)]
        protected IDiffSpread<int> framePresentCount;

        [Input("Frame Queue Max Size", DefaultValue = 10)]
        protected IDiffSpread<int> frameQueueMaxSize;

        [Input("Frame Queue Pool Size", DefaultValue = 10)]
        protected IDiffSpread<int> frameQueuePoolSize;

        [Input("Max Lateness (ms)", DefaultValue = 100)]
        protected IDiffSpread<double> maxLateness;*/

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

        [Output("Is Mode Supported")]
        protected ISpread<bool> isModeSupported;

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

        private bool first = true;
        private CaptureParameters currentParameters = CaptureParameters.Default;
        private CaptureStatistics statistics = new CaptureStatistics();

        private DecklinkCaptureThread captureThread;
        private DX11Resource<YuvToRGBConverter> pixelShaderConverter = new DX11Resource<YuvToRGBConverter>();
        private DX11Resource<YuvToRGBConverterWithTarget> pixelShaderTargetConverter = new DX11Resource<YuvToRGBConverterWithTarget>();
        private DX11Resource<DX11DynamicTexture2D> rawTexture = new DX11Resource<DX11DynamicTexture2D>();
        #endregion fields & pins
        //called when data for any output pin is requested
        public void Evaluate(int SpreadMax)
		{
            if (this.textureOutput[0] == null) { this.textureOutput[0] = new DX11Resource<DX11Texture2D>(); }

            textureOutput.SliceCount = 1;
            statusOutput.SliceCount = 1;
            isModeSupported.SliceCount = 1;


            bool newDevice = false;

            CaptureParameters newParameters = this.captureParameters.DefaultIfNilOrNull(0, CaptureParameters.Default);

            if (this.first == true || this.deviceIndex.IsChanged || this.currentParameters.NeedDeviceReset(newParameters) || this.resetDevice[0])
            {
                if (this.captureThread != null)
                {
                    this.captureThread.Dispose();
                    this.captureThread = null;
                }

                this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0], newParameters); 
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

                        this.captureThread = new DecklinkCaptureThread(this.deviceIndex[0], this.currentParameters);
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
                if (this.captureThread.FramePresenter is IDiscardCounter)
                {
                    this.statistics.FramesDroppedCount = ((IDiscardCounter)this.captureThread.FramePresenter).DiscardCount;
                }

                if (this.captureThread.FramePresenter is TimeQueuedFramePresenter)
                {
                    ((TimeQueuedFramePresenter)this.captureThread.FramePresenter).MaxFrameLateness = this.currentParameters.MaxLateness;
                    this.statistics.CurrentDelay = ((TimeQueuedFramePresenter)this.captureThread.FramePresenter).CurrentDelay;
                }

                this.isModeSupported[0] = this.captureThread.ModeSupport != _BMDDisplayModeSupport.bmdDisplayModeNotSupported;
                this.currentMode[0] = this.captureThread.CurrentDisplayMode.ToString();
                this.width[0] = this.captureThread.Width;
                this.height[0] = this.captureThread.Height;
                this.running[0] = this.captureThread.IsRunning;
                this.modelName[0] = this.captureThread.DeviceInformation.ModelName;
                this.displayName[0] = this.captureThread.DeviceInformation.DisplayName;
                this.statistics.FramesQueueSize = this.captureThread.FramePresenter.QueueSize;
                this.statistics.DelayBetweenFrames = this.captureThread.FrameDelayTime;
                this.statistics.DelayBetweenTextureUpdates = this.captureThread.FrameTextureTime;
                
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

            this.statistics.CurrentFramePresentCount = result.PresentationCount;

            //Perform pixel conversion if applicable
            if (this.currentParameters.OutputMode == TextureOutputMode.UncompressedPS)
            {
                if (isNew)
                {
                    DX11Texture2D converted = this.pixelShaderTargetConverter[context].Apply(inputTexture);
                    this.textureOutput[0][context] = converted;
                }
            }
            else
            {
                this.textureOutput[0][context] = inputTexture;
            }

            if (isNew)
                this.statistics.FramesCopiedCount++;
        }
    }
}
