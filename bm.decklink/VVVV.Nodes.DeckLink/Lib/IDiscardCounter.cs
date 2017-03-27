using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Simple interface to retrieve or reset discard frame count
    /// </summary>
    public interface IDiscardCounter
    {
        /// <summary>
        /// Resets dicard counter
        /// </summary>
        void Reset();

        /// <summary>
        /// Get amount of frames that got discarded
        /// </summary>
        int DiscardCount { get; }
    }
}
