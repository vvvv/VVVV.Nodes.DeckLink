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
        public readonly string ModelName;
        public readonly string DisplayName;
        public readonly bool IsValid;
        public readonly string Message;
        public bool IsAutoModeDetectionSupported;

        private CaptureDeviceInformation(
            string modelName,
            string displayName,
            bool isValid,
            string message,
            bool isAutoModeDetectionSupported)
        {
            this.ModelName = modelName;
            this.DisplayName = displayName;
            this.IsValid = isValid;
            this.Message = message;
            this.IsAutoModeDetectionSupported = isAutoModeDetectionSupported;
        }

        public static CaptureDeviceInformation Invalid(string message)
        {
            return new CaptureDeviceInformation(message, message, false, message, false);
        }

        public static CaptureDeviceInformation FromDevice(IDeckLink device)
        {
            device.GetModelName(out string modelName);
            device.GetDisplayName(out string displayName);
            var deckLinkAttributes = (IDeckLinkProfileAttributes)device;
            deckLinkAttributes.GetFlag(_BMDDeckLinkAttributeID.BMDDeckLinkSupportsInputFormatDetection, out int isAutoDetectSupported);
            bool autoDetecionIsSupported = isAutoDetectSupported != 0;
            bool isValid = true;
            return new CaptureDeviceInformation(modelName, displayName, isValid, "Device Initialized", autoDetecionIsSupported);
        }

    }
}
