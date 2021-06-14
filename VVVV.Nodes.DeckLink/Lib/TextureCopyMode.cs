using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Texture copy mode
    /// </summary>
    public enum TextureCopyMode
    {
        /// <summary>
        /// Texture will be copied as an immutable texture (new one will be created each time)
        /// </summary>
        Immutable,
        /// <summary>
        /// Texture will be copied as dynamic
        /// </summary>
        Dynamic
    }
}
