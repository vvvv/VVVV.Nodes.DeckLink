using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;

namespace VVVV.DeckLink
{
    public class ConvertedFrame : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, int length);

        private IDeckLinkMutableVideoFrame convertedFrame;

        private RawFrameData convertedFrameData = new RawFrameData(1, 1, 4);

        public ConvertedFrame(int initialWidth, int initialHeight)
        {
            this.convertedFrameData = new RawFrameData(initialWidth, initialHeight, initialWidth * initialHeight* 4);
        }

        public RawFrameData ConvertedFrameData
        {
            get { return this.convertedFrameData; }
        }

        public void Copy(DecklinkVideoFrameConverter converter, IDeckLinkVideoInputFrame videoFrame)
        {
            int width = videoFrame.GetWidth();
            int height = videoFrame.GetHeight();
            
            if (width != this.convertedFrameData.Width || height != this.convertedFrameData.Height)
            {
                this.convertedFrameData = this.convertedFrameData.UpdateConvertedFrame(videoFrame);

                if (this.convertedFrame != null)
                {
                    Marshal.ReleaseComObject(this.convertedFrame);
                }
                converter.Device.CreateVideoFrame(width, height, width * 4, _BMDPixelFormat.bmdFormat8BitBGRA, _BMDFrameFlags.bmdFrameFlagDefault, out this.convertedFrame);
            }

            converter.Conversion.ConvertFrame(videoFrame, this.convertedFrame);
            IntPtr ptr;
            this.convertedFrame.GetBytes(out ptr);

            CopyMemory(this.convertedFrameData.DataPointer, ptr, this.convertedFrameData.DataLength);
        }

        public void Dispose()
        {
            if (this.convertedFrameData != null)
            {
                this.convertedFrameData.Dispose();
                this.convertedFrameData = null;
            }
            if (this.convertedFrame != null)
            {
                Marshal.ReleaseComObject(this.convertedFrame);
                this.convertedFrame = null;
            }
        }
    }
}
