using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using VVVV.DeckLink.Utils;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Decklink capture parameters
    /// </summary>
    public sealed class CaptureParameters
    {
        /// <summary>
        /// Video input connection
        /// </summary>
        public VideoInputConnection VideoInputConnection;

        /// <summary>
        /// Texture Output mode
        /// </summary>
        public TextureOutputMode OutputMode;

        /// <summary>
        /// Queue mode when frames are received
        /// </summary>
        public FrameQueueMode FrameQueueMode;

        /// <summary>
        /// Auto detect mode
        /// </summary>
        public bool AutoDetect;

        /// <summary>
        /// Preferred display mode if manual
        /// </summary>
        public _BMDDisplayMode DisplayMode;

        /// <summary>
        /// How many times a frame needs to be presented on the screen.
        /// Setter will enforce a minimum of 1
        /// </summary>
        public int PresentationCount;

        /// <summary>
        /// Maximum size for a frame queue
        /// </summary>
        public NonZeroPositiveInteger FrameQueueMaxSize;

        /// <summary>
        /// Maximum size for a frame ppol
        /// </summary>
        public NonZeroPositiveInteger FrameQueuePoolSize;

        /// <summary>
        /// Maximum lateness of a frame (in milliseconds). 
        /// In timed mode, if a frame timestamp is over that value it will be discarded
        /// </summary>
        public double MaxLateness;

        /// <summary>
        /// Default parameters.
        /// Use auto detect, 10 queue sizes , discard and decklink native conversion
        /// </summary>
        public static CaptureParameters Default
        {
            get
            {
                return new CaptureParameters()
                {
                    AutoDetect = true,
                    FrameQueueMode = FrameQueueMode.DiscardImmutable,
                    DisplayMode = _BMDDisplayMode.bmdModeHD1080p6000,
                    FrameQueueMaxSize = 10,
                    FrameQueuePoolSize = 10,
                    MaxLateness = 100,
                    OutputMode = TextureOutputMode.UncompressedBMD,
                    PresentationCount = 1
                };
            }
        }

        /// <summary>
        /// Compares two set of parameters and indicates if we require a reset.
        /// Any value except lateness and display mode does reset the device.
        /// Oif other parameter is null, we set it to default
        /// </summary>
        /// <param name="other">Other set of paremeters</param>
        /// <returns>True if we need a reset, false otherwise</returns>
        public bool NeedDeviceReset(CaptureParameters other)
        {
            if (other == null)
                other = CaptureParameters.Default;

            return this.AutoDetect != other.AutoDetect
                || this.FrameQueueMaxSize != other.FrameQueueMaxSize
                || this.FrameQueueMode != other.FrameQueueMode
                || this.FrameQueuePoolSize != other.FrameQueuePoolSize
                || this.OutputMode != other.OutputMode
                || this.PresentationCount != other.PresentationCount;
        }
    }
}
