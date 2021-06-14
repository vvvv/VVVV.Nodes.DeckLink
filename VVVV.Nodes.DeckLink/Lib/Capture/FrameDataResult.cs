using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeralTic.DX11.Resources;

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
        /// Current Texture
        /// </summary>
        public readonly DX11Texture2D Texture;

        /// <summary>
        /// Is it a new frame
        /// </summary>
        public readonly bool IsNew;

        /// <summary>
        /// Number of times this frame got set for presentation
        /// </summary>
        public readonly int PresentationCount;

        private FrameDataResult(FrameDataResultType resultType, DecklinkFrameData currentFrame, DX11Texture2D texture, bool isNewFrame, int presentationCount)
        {
            this.ResultType = resultType;
            this.CurrentFrame = currentFrame;
            this.Texture = texture;
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
            return new FrameDataResult(FrameDataResultType.RawImage, currentFrame, null, isNewFrame, presentationCount);
        }


        /// <summary>
        /// Returns a texture (gpu side)
        /// </summary>
        /// <param name="currentFrame">Current frame </param>
        /// <param name="isNewFrame">Tells if frame is new</param>
        /// <param name="presentationCount">Frame presentation count</param>
        /// <returns>Frame data result</returns>
        public static FrameDataResult Texture2D(DX11Texture2D currentTexture, bool isNewFrame, int presentationCount)
        {
            return new FrameDataResult(FrameDataResultType.Texture,null, currentTexture, isNewFrame, presentationCount);
        }
    }
}
