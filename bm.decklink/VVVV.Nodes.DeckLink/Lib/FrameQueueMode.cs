using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Enum to tell how we want to queue frames
    /// </summary>
    public enum FrameQueueMode
    {
        /// <summary>
        /// Discard mode, will always use last frame
        /// </summary>
        Discard,
        /// <summary>
        /// Queued mode, will keep frames in a FIFO fashion
        /// </summary>
        Queued,
        /// <summary>
        /// Timed mode, will discard frames if they timestamp is too old versus presentation time
        /// </summary>
        Timed,
        /// <summary>
        /// Immutable discard mode, use last frames, but perform texture upload in background
        /// </summary>
        DiscardImmutable,
        /// <summary>
        /// Timed mode, will discard frames if they timestamp is too old versus presentation time
        /// Performs upload in immutable fashion
        /// </summary>
        TimedImmutable,
        // <summary>
        // Wait for frames - NOT YET IMPLEMENTED!
        // </summary>
        Wait
    }

}
