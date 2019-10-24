using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using VVVV.PluginInterfaces.V2;

namespace VVVV.DeckLink.Nodes
{
    [PluginInfo(Name = "CaptureParameters", Category = "DeckLink", Version = "Join", Author = "vux", Tags = "blackmagic, capture",
        Help = "Joins a set of parameters for decklink capture")]
    public class CaptureParametersNode : IPluginEvaluate
    {
        [Input("Video Input Connection")]
        protected ISpread<VideoInputConnection> inputConnection;

        [Input("Output Mode")]
        protected ISpread<TextureOutputMode> outputMode;

        [Input("Upload Mode", DefaultEnumEntry = "DiscardImmutable")]
        protected IDiffSpread<FrameQueueMode> queueMode;

        [Input("Auto Detect Mode", DefaultValue = 1)]
        protected IDiffSpread<bool> autoDetect;

        [Input("Display Mode", IsBang = true)]
        protected IDiffSpread<_BMDDisplayMode> displayMode;

        [Input("Frame Present Count", DefaultValue = 1)]
        protected IDiffSpread<int> framePresentCount;

        [Input("Frame Queue Max Size", DefaultValue = 10)]
        protected IDiffSpread<int> frameQueueMaxSize;

        [Input("Frame Queue Pool Size", DefaultValue = 10)]
        protected IDiffSpread<int> frameQueuePoolSize;

        [Input("Max Lateness (ms)", DefaultValue = 100)]
        protected IDiffSpread<double> maxLateness;

        [Output("Output")]
        protected ISpread<CaptureParameters> output;

        public void Evaluate(int SpreadMax)
        {
            for (int i = 0; i < SpreadMax; i++)
            {
                this.output[i] = new CaptureParameters()
                {
                    VideoInputConnection = this.inputConnection[i],
                    AutoDetect = this.autoDetect[i],
                    DisplayMode = this.displayMode[i],
                    FrameQueueMaxSize = this.frameQueueMaxSize[i],
                    FrameQueueMode = this.queueMode[i],
                    FrameQueuePoolSize = this.frameQueuePoolSize[i],
                    MaxLateness = this.maxLateness[i],
                    OutputMode = this.outputMode[i],
                    PresentationCount = this.framePresentCount[i]
                };
            }
        }
    }
}
