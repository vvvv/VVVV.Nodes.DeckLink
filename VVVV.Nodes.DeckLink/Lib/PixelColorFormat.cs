using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Pixel Format
    /// </summary>
    public enum PixelColorFormat
    {
        /// <summary>
        /// Query the BlackMagic API using YUV 8bit pixel format
        /// </summary>
        YUV8Bit,
        /// <summary>
        /// Query the BlackMagic API using RGB 8bit pixel format
        /// </summary>
        RGB8Bit
    }
}
