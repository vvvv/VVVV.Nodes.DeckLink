using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Frame result to return for capture
    /// </summary>
    public struct FrameDataResult
    {
        /// <summary>
        /// Current frame
        /// </summary>
        public readonly DecklinkFrameData CurrentFrame;

        /// <summary>
        /// Is it a new frame
        /// </summary>
        public readonly bool IsNew;

        /// <summary>
        /// Number of times this frame got set for presentation
        /// </summary>
        public readonly int PresentationCount;

        public FrameDataResult(DecklinkFrameData currentFrame, bool isNewFrame, int presentationCount)
        {
            this.CurrentFrame = currentFrame;
            this.IsNew = isNewFrame;
            this.PresentationCount = presentationCount;
        }
    }
}
