using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;

namespace VVVV.DeckLink.Presenters
{

    /// <summary>
    /// Interface to deal with various presentation modes
    /// </summary>
    public interface IDecklinkFramePresenter
    {
        /// <summary>
        /// Push a new frame when received by the device
        /// </summary>
        /// <param name="videoFrame">new video frame</param>
        void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion);

        /// <summary>
        /// Get frame that should be presented
        /// </summary>
        /// <returns></returns>
        FrameDataResult GetPresentationFrame();
    }

    public interface IDecklinkQueuedFramePresenter : IDecklinkFramePresenter
    {
        /// <summary>
        /// Queue size (if relevant, else just discard)
        /// </summary>
        int QueueSize { get; }
    }
}
