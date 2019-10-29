using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using VVVV.DeckLink;
using VVVV.Utils.VMath;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Device factory
    /// </summary>
    public class DeviceFactory
    {
        private static List<int> usedDevices = new List<int>();

        public readonly IDeckLink DeckLinkDevice;
        public readonly IDeckLinkInput InputDevice;
        public readonly CaptureDeviceInformation DeviceInformation;
        public readonly int slicedInput;

        public DeviceFactory(int index)
        {
            IDeckLink deckLink;
            IDeckLinkIterator iterator = new CDeckLinkIterator();
            List<IDeckLink> deviceList = new List<IDeckLink>();

            while (true)
            {
                iterator.Next(out deckLink);
                if (deckLink == null)
                    break;
                else
                    deviceList.Add(deckLink);
            }

            if (deviceList.Count > 0)
            {
                this.slicedInput = VMath.Zmod(index, deviceList.Count);
                if (!usedDevices.Contains(slicedInput))
                {
                    usedDevices.Add(slicedInput);
                    this.InputDevice = deviceList[slicedInput] as IDeckLinkInput;
                    this.DeckLinkDevice = deviceList[slicedInput];
                    this.DeviceInformation = CaptureDeviceInformation.FromDevice(this.DeckLinkDevice);
                }
                else
                {
                    this.InputDevice = null;
                    this.DeviceInformation = CaptureDeviceInformation.Invalid("Device already in use");
                }
                Marshal.ReleaseComObject(iterator);
            }
            else
            {
                this.InputDevice = null;
                this.DeviceInformation = CaptureDeviceInformation.Invalid("Device not found");
            }
        }

        /// <summary>
        /// Releases a device from static list
        /// </summary>
        /// <param name="inputIndex">Input device index</param>
        public static void ReleaseDevice(int inputIndex)
        {
            usedDevices.Remove(inputIndex);
        }
    }

}
