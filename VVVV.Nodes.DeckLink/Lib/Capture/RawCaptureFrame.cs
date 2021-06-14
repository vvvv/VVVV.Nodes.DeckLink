using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using FeralTic.DX11.Resources;

namespace VVVV.DeckLink
{
    public class RawFrameData : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, int length);

        private IntPtr rawCompressedData = IntPtr.Zero;
        private int rawCompressedDataLength = 0;
        private int width;
        private int height;

        public RawFrameData(int width, int height, int dataSize)
        {
            this.width = width;
            this.height = height;
            this.rawCompressedDataLength = dataSize;
            this.rawCompressedData = Marshal.AllocHGlobal(dataSize);
        }

        public IntPtr DataPointer
        {
            get { return this.rawCompressedData; }
        }

        public int DataLength
        {
            get { return this.rawCompressedDataLength; }
        }

        public int Width
        {
            get { return this.width; }
        }

        public int Height
        {
            get { return this.height; }
        }
            
        public void Dispose()
        {
            if (rawCompressedData != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(rawCompressedData);
                rawCompressedData = IntPtr.Zero;
            }
        }
        public void Copy(IDeckLinkVideoInputFrame videoFrame)
        {
            IntPtr dataPointer;
            videoFrame.GetBytes(out dataPointer);
            CopyMemory(this.rawCompressedData, dataPointer, this.rawCompressedDataLength);
        }
    }

    public static class RawCaptureFrameExtentionMethods
    {
        public static RawFrameData UpdateRawFrame(this RawFrameData frame, IDeckLinkVideoInputFrame videoFrame, int pixelFormatDivisor = 2)
        {
            int width = videoFrame.GetWidth();
            int height = videoFrame.GetHeight();
            int frameSize = width / pixelFormatDivisor * 4 * height;
            if (frameSize != frame.DataLength)
            {
                frame.Dispose();
                return new RawFrameData(width, height, frameSize);
            }
            else
                return frame;
        }

        public static RawFrameData UpdateConvertedFrame(this RawFrameData frame, IDeckLinkVideoInputFrame videoFrame)
        {
            int width = videoFrame.GetWidth();
            int height = videoFrame.GetHeight();
            int frameSize = width * 4 * height;
            if (frameSize != frame.DataLength)
            {
                frame.Dispose();
                return new RawFrameData(width, height, frameSize);
            }
            else
                return frame;
        }

        public static void MapAndCopyFrame(this RawFrameData frame, DX11DynamicTexture2D texture)
        {
            if (frame.Width * 4 == texture.GetRowPitch())
                texture.WriteData(frame.DataPointer, frame.DataLength);
            else
                texture.WriteDataPitch(frame.DataPointer, frame.DataLength, 4);
        }
    }
}
