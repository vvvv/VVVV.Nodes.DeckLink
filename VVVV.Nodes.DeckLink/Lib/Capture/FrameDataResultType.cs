using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Result type for frame data
    /// </summary>
    public enum FrameDataResultType
    {
        /// <summary>
        /// Returns a raw image
        /// </summary>
        RawImage,
        /// <summary>
        /// Returns a texture
        /// </summary>
        Texture
    }
}
