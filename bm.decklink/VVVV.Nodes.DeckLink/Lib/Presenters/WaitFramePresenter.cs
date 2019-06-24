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
        private IDeckLinkVideoInputFrame inputVideoFrame;
        private bool peformConvertion = false;
        // @TODO: evaluate the need of this! Shorten Thread.Sleep() by this factor
        private float overheadModifier = 0.00f;

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
            long timeScale = 1000;
            long frameTime, frameDuration;
            // @TODO check difference between frame timing functions 
            videoFrame.GetHardwareReferenceTimestamp(timeScale, out frameTime, out frameDuration);
            //videoFrame.GetStreamTime(out frameTime, out frameDuration, timeScale);
            int delay = Convert.ToInt32(frameDuration);
            WaitHandle waitHandle = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(new WaitCallback(state => WaitForPushFrame(state, delay)), waitHandle);
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
            waitHandle.WaitOne();
        }

        void WaitForPushFrame(Object state, int duration)
        {
            ManualResetEvent ev = (ManualResetEvent)state;
            Thread.Sleep(duration - Convert.ToInt32(duration * this.overheadModifier));
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
