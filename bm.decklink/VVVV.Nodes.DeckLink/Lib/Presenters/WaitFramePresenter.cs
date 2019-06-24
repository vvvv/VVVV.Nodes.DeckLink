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
        private bool isNewFrame = true;
        // @TODO: evaluate the need of this! Shorten Thread.Sleep() by this factor
        WaitHandle waitHandle = new AutoResetEvent(false);

        public int QueueSize
        {
            get { return 0; }
        }

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
            var result = FrameDataResult.RawImage(this.frame, isNewFrame, 1);
            this.isNewFrame = true;
            return result;
        }

        public void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
            // Delay execution
            //long timeScale = 1000;
            //long frameTime, frameDuration;
            // @TODO check difference between frame timing functions 
            //videoFrame.GetHardwareReferenceTimestamp(timeScale, out frameTime, out frameDuration);
            //videoFrame.GetStreamTime(out frameTime, out frameDuration, timeScale);
            //int delay = Convert.ToInt32(frameDuration);
            ThreadPool.QueueUserWorkItem(new WaitCallback(state => WaitForPushFrame(state, 0, videoFrame, performConvertion)), waitHandle);
            WaitHandle.WaitAll(new WaitHandle[] { waitHandle });
            waitHandle.Dispose();
        }

        void WaitForPushFrame(Object state, int duration, IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
            AutoResetEvent ev = (AutoResetEvent)state;
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
            ev.Set();
        }

        public void Dispose()
        {
            if (this.frame != null)
            {
                this.frame.Dispose();
                this.frame = null;
            }
        }
    }
}
