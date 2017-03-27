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
        /// Result type
        /// </summary>
        public readonly FrameDataResultType ResultType;

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

        private FrameDataResult(FrameDataResultType resultType, DecklinkFrameData currentFrame, bool isNewFrame, int presentationCount)
        {
            this.ResultType = resultType;
            this.CurrentFrame = currentFrame;
            this.IsNew = isNewFrame;
            this.PresentationCount = presentationCount;
        }

        /// <summary>
        /// Returns a raw image (cpu side)
        /// </summary>
        /// <param name="currentFrame">Current frame </param>
        /// <param name="isNewFrame">Tells if frame is new</param>
        /// <param name="presentationCount">Frame presentation count</param>
        /// <returns>Frame data result</returns>
        public static FrameDataResult RawImage(DecklinkFrameData currentFrame, bool isNewFrame, int presentationCount)
        {
            return new FrameDataResult(FrameDataResultType.RawImage, currentFrame, isNewFrame, presentationCount);
        }
    }
}
