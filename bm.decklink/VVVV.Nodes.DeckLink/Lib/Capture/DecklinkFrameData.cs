using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using VVVV.Decklink.Utils;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Decklink full frame data, contains back end for both converted and non converted frame data
    /// </summary>
    public class DecklinkFrameData : IDisposable
    {
        
        private RawFrameData rawCapturedFrame;
        private ConvertedFrame convertedFrame;
        private TimeSpan receivedTimeStamp;

        public RawFrameData RawFrameData
        {
            get { return this.rawCapturedFrame; }
        }
        public RawFrameData ConvertedFrameData
        {
            get { return this.convertedFrame.ConvertedFrameData; }
        }

        public TimeSpan ReceivedTimeStamp
        {
            get { return this.receivedTimeStamp; }
        }

        public DecklinkFrameData() : this(1,1)
        {

        }

        public DecklinkFrameData(int initialWidth, int initialHeight)
        {
            this.rawCapturedFrame = new RawFrameData(initialWidth, initialHeight, initialWidth * initialHeight * 2);
            this.convertedFrame = new ConvertedFrame(initialWidth, initialHeight);
        }

        private void Update(IDeckLinkVideoInputFrame videoFrame)
        {
            this.rawCapturedFrame = this.rawCapturedFrame.UpdateRawFrame(videoFrame);
        }

        public void UpdateAndCopy(IDeckLinkVideoInputFrame videoFrame)
        {
            this.receivedTimeStamp = Timer.Elapsed;
            this.Update(videoFrame);
            this.rawCapturedFrame.Copy(videoFrame);
        }

        public void UpdateAndConvert(DecklinkVideoFrameConverter converter, IDeckLinkVideoInputFrame videoFrame)
        {
            this.receivedTimeStamp = Timer.Elapsed;
            this.Update(videoFrame);
            this.convertedFrame.Copy(converter, videoFrame);
        }

        public void Dispose()
        {
            if (rawCapturedFrame != null)
            {
                this.rawCapturedFrame.Dispose();
                this.rawCapturedFrame = null;
            }
            if (this.convertedFrame != null)
            {
                this.convertedFrame.Dispose();
                this.convertedFrame = null;
            }

        }
    }
}
