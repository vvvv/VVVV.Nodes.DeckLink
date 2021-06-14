using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Information about a capture device
    /// </summary>
    public sealed class CaptureDeviceInformation
    {
        public string ModelName;
        public string DisplayName;
        public bool IsValid;
        public string Message;
        public bool IsAutoModeDetectionSupported;
        private readonly IDeckLink device;

        private CaptureDeviceInformation(IDeckLink device, string message)
        {
            if (device == null)
            {
                this.ModelName = "Unknown";
                this.DisplayName = "Unknown";
                this.IsValid = false;
                this.Message = "Device seems to be occupied already.";
                this.IsAutoModeDetectionSupported = false;
            }
            else
            {
                this.device = device;
                this.device.GetModelName(out this.ModelName);
                device.GetDisplayName(out this.DisplayName);
                var deckLinkAttributes = (IDeckLinkProfileAttributes)this.device;
                deckLinkAttributes.GetFlag(_BMDDeckLinkAttributeID.BMDDeckLinkSupportsInputFormatDetection, out int isAutoDetectSupported);
                bool autoDetecionIsSupported = isAutoDetectSupported != 0;
                this.IsValid = true;
                this.Message = message;
                this.IsAutoModeDetectionSupported = autoDetecionIsSupported;
            }
        }

        public CaptureDeviceInformation Invalid(string message)
        {
            this.Message = message;
            this.IsValid = false;
            return this;
        }

        public CaptureDeviceInformation Valid(string message)
        {
            this.Message = message;
            this.IsValid = true;
            return this;
        }

        public static CaptureDeviceInformation FromDevice(IDeckLink device)
        {
            return new CaptureDeviceInformation(device, "Initialized");
        }
    }
}
