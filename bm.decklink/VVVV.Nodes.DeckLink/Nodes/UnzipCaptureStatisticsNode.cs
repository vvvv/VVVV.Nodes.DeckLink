using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VVVV.PluginInterfaces.V2;

namespace VVVV.DeckLink.Nodes
{
    [PluginInfo(Name = "Unzip", Category = "DeckLink", Version = "CaptureStatistics", Author = "vux", Tags = "blackmagic, capture",
        Help ="Unzips all statistics for a decklink capture")]
    public class UnzipStatisticsNode : IPluginEvaluate
    {
        [Input("Input")]
        protected ISpread<CaptureStatistics> input;

        [Output("Frames Captured Count")]
        protected ISpread<int> framesCapturedCount;

        [Output("Frames Copied Count")]
        protected ISpread<int> framesCopiedCount;

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

        public void Evaluate(int SpreadMax)
        {
            this.currentDelay.SliceCount = SpreadMax;
            this.currentFramePresentCount.SliceCount = SpreadMax;
            this.delayBetweenFrames.SliceCount = SpreadMax;
            this.delayBetweenTextureUpdates.SliceCount = SpreadMax;
            this.framesCapturedCount.SliceCount = SpreadMax;
            this.framesCopiedCount.SliceCount = SpreadMax;
            this.framesDroppedCount.SliceCount = SpreadMax;
            this.framesQueueSize.SliceCount = SpreadMax;

            for (int i = 0; i < SpreadMax; i++)
            {
                CaptureStatistics stats = this.input[i];
                this.currentDelay[i] = stats.CurrentDelay;
                this.currentFramePresentCount[i] = stats.CurrentFramePresentCount;
                this.delayBetweenFrames[i] = stats.DelayBetweenFrames;
                this.delayBetweenTextureUpdates[i] = stats.DelayBetweenTextureUpdates;
                this.framesCapturedCount[i] = stats.FramesCapturedCount;
                this.framesCopiedCount[i] = stats.FramesCopiedCount;
                this.framesDroppedCount[i] = stats.FramesDroppedCount;
                this.framesQueueSize[i] = stats.FramesQueueSize;
            }
        }
    }
}
