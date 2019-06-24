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
        /// DEPRECATED
        /// Discard mode, will always use last frame
        /// </summary>
        Discard_DEPRECATED,
        /// <summary>
        /// DEPRECATED
        /// Queued mode, will keep frames in a FIFO fashion
        /// </summary>
        Queued_DEPRECATED,
        /// <summary>
        /// DEPRECATED
        /// Timed mode, will discard frames if they timestamp is too old versus presentation time
        /// </summary>
        Timed_DEPRECATED,
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
        // NOT YET IMPLEMENTED!
        // Wait for frames.
        // </summary>
        Wait
    }

}
