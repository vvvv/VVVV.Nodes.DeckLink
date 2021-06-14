using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Texture output mode
    /// </summary>
    public enum TextureOutputMode
    {
        /// <summary>
        /// Uncompress texture using pixel shader
        /// </summary>
        UncompressedPS,
        /// <summary>
        /// Uncompress texture using decklink native converter
        /// </summary>
        UncompressedBMD,
        /// <summary>
        /// Outputs raw yuv texture
        /// </summary>
        CompressedYUV,
    }
}
