using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using System.Threading;

namespace VVVV.DeckLink.Presenters
{
    /// <summary>
    /// Wait mode frame presentation, always wait for last frame, regardless of what happens
    /// </summary> 
    public class WaitFramePresenter : IDecklinkFramePresenter, IDisposable
    {
        private readonly DecklinkVideoFrameConverter videoConverter;
        private DecklinkFrameData frame = new DecklinkFrameData();

        public WaitFramePresenter(DecklinkVideoFrameConverter videoConverter)
        {
            if (videoConverter == null)
            {
                throw new ArgumentNullException("videoConverter");
            }
            this.videoConverter = videoConverter;
        }

        public FrameDataResult GetPresentationFrame()
        {
            var isNewFrame = true;
            var result = FrameDataResult.RawImage(this.frame, isNewFrame, 1);
            return result;
        }

        public void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
            if (performConvertion)
            {
                this.frame.UpdateAndConvert(this.videoConverter, videoFrame);
            }
            else
            {
                this.frame.UpdateAndCopy(videoFrame);
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);
        }

        public void Dispose()
        {
            this.frame.Dispose();
            this.frame = null;
        }
    }
}
