using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using VVVV.Decklink.Utils;
using VVVV.DeckLink.Direct3D11;
using VVVV.DX11.Nodes;

namespace VVVV.DeckLink.Presenters
{
    /// <summary>
    /// Queued frame presenter, enforces a minimum presentation count
    /// </summary>
    public class TimeQueuedImmutableFramePresenter : IDecklinkFramePresenter, 
        IDisposable, 
        IFlushable, 
        IDiscardCounter, 
        ILatencyReporter,
        IStatusQueueReporter
    {
        private class TexturePresentationFrame
        {
            private int presentationCount;
            private DecklinkFrameData frameData;
            private DX11Texture2D textureData;

            public TexturePresentationFrame(DecklinkTextureFrameData textureFrameData)
            {
                this.frameData = textureFrameData.RawFrame;
                this.textureData = textureFrameData.Texture;
            }

            public DecklinkFrameData Frame
            {
                get { return this.frameData; }
            }

            public DX11Texture2D Texture
            {
                get { return this.textureData; }
            }

            public int PresentationCount
            {
                get { return this.presentationCount; }
            }

            public void IncrementPresentationCount()
            {
                this.presentationCount++;
            }

            public void DisposeTexture()
            {
                if (textureData != null)
                {
                    textureData.Resource.Dispose();
                    textureData.SRV.Dispose();
                    textureData = null;
                }
            }
        }

        private readonly int maxQueueSize;
        private readonly int initialPoolSize;
        private readonly int presentationCount;
        private readonly DecklinkVideoFrameConverter videoConverter;
        private readonly DX11RenderContext renderDevice;

        private FramePool framePool;
        private Queue<DecklinkTextureFrameData> frameQueue = new Queue<DecklinkTextureFrameData>();

        private TexturePresentationFrame currentPresentationFrame;
        private object syncRoot = new object();

        private int discardCount;

        private double currentDelay;

        public double MaxFrameLateness
        {
            private get; set;
        }

        public int QueueSize
        {
            get { return this.frameQueue.Count; }
        }

        public int DiscardCount
        {
            get { return this.discardCount; }
        }

        public double CurrentDelay
        {
            get { return this.currentDelay; }
        }

        public IReadOnlyList<TimeSpan> QueueData
        {
            get
            {
                List<TimeSpan> queueData = new List<TimeSpan>();
                lock (syncRoot)
                {
                    queueData.AddRange(this.frameQueue.Select(f => f.RawFrame.ReceivedTimeStamp));
                }
                return queueData;
            }
        }

        private DecklinkTextureFrameData DequeueFrame()
        {
            TimeSpan currentTime = Timer.Elapsed;
            //double deltaMilli = currentTime 
            lock (syncRoot)
            {
                if (this.frameQueue.Count == 0)
                {
                    return null;
                }
                else
                {
                    var frame = this.frameQueue.Dequeue();
                    var delta = currentTime - frame.RawFrame.ReceivedTimeStamp;

                    while (this.frameQueue.Count > 0) // if queue now empty, just return that specific frame
                    {
                        delta = currentTime - frame.RawFrame.ReceivedTimeStamp;

                        //if this frame is too late
                        if (delta.TotalMilliseconds >= this.MaxFrameLateness)
                        {
                            if (frame != null)
                            {
                                frame.DisposeTexture();
                            }
                            //Recycle and get new frame
                            this.framePool.Recycle(frame.RawFrame);
                            frame = this.frameQueue.Dequeue(); 
                        }
                        else
                        {
                            this.currentDelay = delta.TotalMilliseconds;
                            return frame;
                        }
                    }

                    this.currentDelay = delta.TotalMilliseconds;
                    return frame;

                }
            }
        }

        public TimeQueuedImmutableFramePresenter(DX11RenderContext renderDevice, DecklinkVideoFrameConverter videoConverter, int presentationCount, int initialPoolSize, int maxQueueSize)
        {
            if (renderDevice == null)
                throw new ArgumentNullException("renderDevice");
            if (videoConverter == null)
                throw new ArgumentNullException("videoConverter");
            if (presentationCount < 1)
                throw new ArgumentOutOfRangeException("presentationCount", "Muse be at least one");

            this.renderDevice = renderDevice;
            this.videoConverter = videoConverter;
            this.presentationCount = presentationCount;
            this.framePool = new FramePool(initialPoolSize);
            this.initialPoolSize = initialPoolSize;
            this.maxQueueSize = maxQueueSize;
        }

        public FrameDataResult GetPresentationFrame()
        {
            //Return a null frame, maybe create an empty black one?
            if (this.currentPresentationFrame == null && this.frameQueue.Count == 0)
            {
                return FrameDataResult.RawImage(null, false, 0);
            }

            //Some frames are in the queue
            if (this.frameQueue.Count > 0)
            {
                //no frame yet, get one from the queue
                if (this.currentPresentationFrame == null)
                {
                    var frame = this.DequeueFrame();
                    this.currentPresentationFrame = new TexturePresentationFrame(frame);
                    this.currentPresentationFrame.IncrementPresentationCount();
                    return FrameDataResult.Texture2D(this.currentPresentationFrame.Texture, true, this.currentPresentationFrame.PresentationCount);
                }
                else
                {
                    int currentCount = this.currentPresentationFrame.PresentationCount;

                    //Discard that frame and get a new one
                    if (currentCount > this.presentationCount)
                    {
                        lock (syncRoot)
                        {
                            currentPresentationFrame.DisposeTexture();
                            this.framePool.Recycle(this.currentPresentationFrame.Frame);
                        }

                        var frame = this.DequeueFrame();
                        this.currentPresentationFrame = new TexturePresentationFrame(frame);
                        this.currentPresentationFrame.IncrementPresentationCount();
                        return FrameDataResult.Texture2D(this.currentPresentationFrame.Texture, true, this.currentPresentationFrame.PresentationCount);
                    }
                    else
                    {
                        this.currentPresentationFrame.IncrementPresentationCount();
                        return FrameDataResult.Texture2D(this.currentPresentationFrame.Texture, false, this.currentPresentationFrame.PresentationCount);
                    }
                }
            }
            else
            {
                //Still mark that frame as a new present
                this.currentPresentationFrame.IncrementPresentationCount();
                return FrameDataResult.Texture2D(this.currentPresentationFrame.Texture, false, this.currentPresentationFrame.PresentationCount);
            }
        }

        public void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
            if (this.renderDevice.Device.Disposed)
                return;

            //Drop frame if queue is full
            if (this.frameQueue.Count >= this.maxQueueSize)
            {
                this.discardCount++;
                return;
            }
            //Create a new frame and push to the queue
            DecklinkFrameData frameData;
            lock (syncRoot)
            {
                frameData = this.framePool.Acquire();
            }

            DX11Texture2D newTexture;
            if (performConvertion)
            {
                frameData.UpdateAndConvert(this.videoConverter, videoFrame);
                newTexture = ImmutableTextureFactory.CreateConvertedFrame(this.renderDevice, frameData.ConvertedFrameData);
            }
            else
            {
                frameData.UpdateAndCopy(videoFrame);
                newTexture = ImmutableTextureFactory.CreateRawFrame(this.renderDevice, frameData.RawFrameData);
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);

            lock (syncRoot)
            {
                this.frameQueue.Enqueue(new DecklinkTextureFrameData(frameData, newTexture));
            }
            
        }

        public void Dispose()
        {
            lock (syncRoot)
            {
                this.frameQueue.Clear();
                this.framePool.Dispose();
            }
            if (this.currentPresentationFrame != null)
            {
                this.currentPresentationFrame.Frame.Dispose();
                this.currentPresentationFrame.Texture.Resource.Dispose();
                this.currentPresentationFrame.Texture.SRV.Dispose();
                this.currentPresentationFrame = null;
            }
            this.framePool.Dispose();
            
        }

        public void Flush()
        {
            //Create a new pool
            lock (syncRoot)
            {
                //Preserve current frame
                this.framePool.Compact(this.currentPresentationFrame != null ? this.currentPresentationFrame.Frame : null);

                this.frameQueue.Clear();
            }
        }

        public void Reset()
        {
            this.discardCount = 0;
        }
    }
}
