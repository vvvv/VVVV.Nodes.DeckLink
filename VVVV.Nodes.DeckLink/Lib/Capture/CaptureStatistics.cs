using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Contains capture statistics for a device
    /// </summary>
    public struct CaptureStatistics
    {
        /// <summary>
        /// Number of frames captures by the device
        /// </summary>
        public int FramesCapturedCount;

        /// <summary>
        /// Number of frames uploaded to the graphics card
        /// </summary>
        public int FramesCopiedCount;

        /// <summary>
        /// Current size of the queued frames, if applicable
        /// </summary>
        public int FramesQueueSize;

        /// <summary>
        /// Number of times the current frame has been presented to the screen
        /// </summary>
        public int CurrentFramePresentCount;

        /// <summary>
        /// Number of frames that got dropped
        /// </summary>
        public int FramesDroppedCount;

        /// <summary>
        /// Delay between two frames (this is calculated as soon as a new frame is received, so that should be framerate
        /// </summary>
        public double DelayBetweenFrames;

        /// <summary>
        /// Delay between two update calls, please note that it does not say if frame got update or not
        /// </summary>
        public double DelayBetweenTextureUpdates;

        /// <summary>
        /// Delay between frame is receive on decklink api till time we give back control
        /// </summary>
        public double FrameProcessTime;

        /// <summary>
        /// Delay between the time a frame got acquired and time is has been assigned for presentation
        /// </summary>
        public double CurrentDelay;

        /// <summary>
        /// Frames per second queried via DeckLink SDK
        /// </summary>
        public int DeckLinkFPS;

        public void Reset()
        {
            this.FramesCapturedCount = 0;
            this.FramesCopiedCount = 0;
            this.FramesQueueSize = 0;
            this.CurrentFramePresentCount = 0;
            this.FramesDroppedCount = 0;
            this.DelayBetweenFrames = 0.0;
            this.DelayBetweenTextureUpdates = 0.0;
            this.FrameProcessTime = 0.0;
            this.CurrentDelay = 0;
            this.DeckLinkFPS = 0;
        }
    }
}
