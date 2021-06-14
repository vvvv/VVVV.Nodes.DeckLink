using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;

namespace VVVV.DeckLink
{
    public class DecklinkVideoFrameConverter
    {
        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "CopyMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, int length);

        private IDeckLinkVideoConversion converter;
        private IDeckLinkOutput devout;

        public IDeckLinkOutput Device
        {
            get { return this.devout; }
        }

        public IDeckLinkVideoConversion Conversion
        {
            get { return this.converter; }
        }


        public DecklinkVideoFrameConverter(IDeckLink device)
        {
            devout = device as IDeckLinkOutput;
            this.converter = new CDeckLinkVideoConversion();
        }
    }
}
