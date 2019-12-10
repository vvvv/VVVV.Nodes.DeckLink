using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using VVVV.PluginInterfaces.V2;

namespace VVVV.DeckLink.Nodes
{
    [PluginInfo(Name = "CaptureParameters", 
                Category = "DeckLink", 
                Version = "Join", 
                Author = "vux", 
                Tags = "blackmagic, capture",
                Help = "Joins a set of parameters for decklink capture")]
    public class CaptureParametersNode : IPluginEvaluate
    {
        [Input("Output Mode")]
        protected ISpread<TextureOutputMode> FIn_OutputMode;

        [Input("Upload Mode", DefaultEnumEntry = "DiscardImmutable")]
        protected IDiffSpread<FrameQueueMode> FIn_UploadMode;

        [Input("PixelFormat", DefaultEnumEntry = "YUV8Bit")]
        protected IDiffSpread<PixelColorFormat> FIn_PixelFormat;

        [Input("Auto Detect Mode", DefaultValue = 1)]
        protected IDiffSpread<bool> FIn_AutoDetectMode;

        [Input("Display Mode", IsBang = true)]
        protected IDiffSpread<_BMDDisplayMode> FIn_DisplayMode;

        [Input("Frame Present Count", DefaultValue = 1)]
        protected IDiffSpread<int> FIn_FramePresentCount;

        [Input("Frame Queue Max Size", DefaultValue = 10)]
        protected IDiffSpread<int> FIn_FrameQueueMaxSize;

        [Input("Frame Queue Pool Size", DefaultValue = 10)]
        protected IDiffSpread<int> FIn_FrameQueuePoolSize;

        [Input("Max Lateness (ms)", DefaultValue = 100)]
        protected IDiffSpread<double> FIn_MaxLateness;

        [Output("Output")]
        protected ISpread<CaptureParameters> FOut_CaptureParameters;

        public void Evaluate(int SpreadMax)
        {
            for (int i = 0; i < SpreadMax; i++)
            {
                this.FOut_CaptureParameters[i] = new CaptureParameters()
                {
                    AutoDetect = this.FIn_AutoDetectMode[i],
                    DisplayMode = this.FIn_DisplayMode[i],
                    FrameQueueMaxSize = this.FIn_FrameQueueMaxSize[i],
                    FrameQueueMode = this.FIn_UploadMode[i],
                    FrameQueuePoolSize = this.FIn_FrameQueuePoolSize[i],
                    MaxLateness = this.FIn_MaxLateness[i],
                    OutputMode = this.FIn_OutputMode[i],
                    PresentationCount = this.FIn_FramePresentCount[i],
                    PixelFormat = this.FIn_PixelFormat[i]
                };
            }
        }
    }
}
