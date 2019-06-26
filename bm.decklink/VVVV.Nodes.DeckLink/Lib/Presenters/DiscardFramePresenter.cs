using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;

namespace VVVV.DeckLink.Presenters
{
    /// <summary>
    /// Discard mode frame presentation, always present last frame, regardless of what happened
    /// </summary> 
    public class DiscardFramePresenter : IDecklinkQueuedFramePresenter, IDisposable, IDiscardCounter
    {
        #region Variables
        private bool isNewFrame = false;
        private bool lastFramePresented = false;
        private int discardCount = 0;
        private readonly DecklinkVideoFrameConverter videoConverter;
        private DecklinkFrameData frame = new DecklinkFrameData();
        #endregion

        #region Properties
        public int QueueSize
        {
            get { return 0; }
        }

        public int DiscardCount
        {
            get { return this.discardCount; }
        }
        #endregion

        public DiscardFramePresenter(DecklinkVideoFrameConverter videoConverter)
        {
            if (videoConverter == null) throw new ArgumentNullException("videoConverter");
            this.videoConverter = videoConverter;
        }

        public FrameDataResult GetPresentationFrame()
        {
            var result = FrameDataResult.RawImage(this.frame, isNewFrame, 1);
            this.isNewFrame = false;
            this.lastFramePresented = true;
            return result;
        }

        public void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
            if (this.lastFramePresented == false)
            {
                this.discardCount++;
            }
            if (performConvertion)
            {
                this.frame.UpdateAndConvert(this.videoConverter, videoFrame);
            }
            else
            {
                this.frame.UpdateAndCopy(videoFrame);
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);
            this.isNewFrame = true;
            this.lastFramePresented = false;
        }

        public void Dispose()
        {
            if (this.frame != null)
            {
                this.frame.Dispose();
                this.frame = null;
            }
        }

        public void Reset()
        {
            this.discardCount = 0;
        }
    }
}
