﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using VVVV.Decklink.Utils;
using VVVV.DX11.Nodes;

namespace VVVV.DeckLink.Presenters
{
    /// <summary>
    /// Queued frame presenter, enforces a minimum presentation count
    /// </summary>
    public class TimeQueuedFramePresenter : IDecklinkQueuedFramePresenter, IDisposable, IFlushable, IDiscardCounter, ILatencyReporter
    {
        private class PresentationFrame
        {
            private int presentationCount;
            private DecklinkFrameData frameData;

            public PresentationFrame(DecklinkFrameData frameData)
            {
                this.frameData = frameData;
            }

            public DecklinkFrameData Frame
            {
                get { return this.frameData; }
            }

            public int PresentationCount
            {
                get { return this.presentationCount; }
            }

            public void IncrementPresentationCount()
            {
                this.presentationCount++;
            }
        }

        private readonly int maxQueueSize;
        private readonly int initialPoolSize;
        private readonly int presentationCount;
        private readonly DecklinkVideoFrameConverter videoConverter;
        private FramePool framePool;
        private double currentDelay;

        private Queue<DecklinkFrameData> frameQueue = new Queue<DecklinkFrameData>();

        private PresentationFrame currentPresentationFrame;
        private object syncRoot = new object();

        private int discardCount;

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

        public double MaxFrameLateness
        {
            private get; set;
        }

        private DecklinkFrameData DequeueFrame()
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
                    var delta = currentTime - frame.ReceivedTimeStamp;

                    while (this.frameQueue.Count > 0) // if queue now empty, just return that specific frame
                    {
                        delta = currentTime - frame.ReceivedTimeStamp;

                        //if this frame is too late
                        if (delta.TotalMilliseconds >= this.MaxFrameLateness)
                        {
                            
                            //Recycle and get new frame
                            this.framePool.Recycle(frame);
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

        public TimeQueuedFramePresenter(DecklinkVideoFrameConverter videoConverter, int presentationCount, int initialPoolSize, int maxQueueSize)
        {
            if (videoConverter == null)
                throw new ArgumentNullException("videoConverter");

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
                    this.currentPresentationFrame = new PresentationFrame(frame);
                    this.currentPresentationFrame.IncrementPresentationCount();
                    return FrameDataResult.RawImage(this.currentPresentationFrame.Frame, true, this.currentPresentationFrame.PresentationCount);
                }
                else
                {
                    int currentCount = this.currentPresentationFrame.PresentationCount;

                    //Discard that frame and get a new one
                    if (currentCount > this.presentationCount)
                    {
                        lock (syncRoot)
                        {
                            this.framePool.Recycle(this.currentPresentationFrame.Frame);
                        }

                        var frame = this.DequeueFrame();
                        this.currentPresentationFrame = new PresentationFrame(frame);
                        this.currentPresentationFrame.IncrementPresentationCount();
                        return FrameDataResult.RawImage(this.currentPresentationFrame.Frame, true, this.currentPresentationFrame.PresentationCount);
                    }
                    else
                    {
                        this.currentPresentationFrame.IncrementPresentationCount();
                        return FrameDataResult.RawImage(this.currentPresentationFrame.Frame, false, this.currentPresentationFrame.PresentationCount);
                    }
                }
            }
            else
            {
                //Still mark that frame as a new present
                this.currentPresentationFrame.IncrementPresentationCount();
                return FrameDataResult.RawImage(this.currentPresentationFrame.Frame, false, this.currentPresentationFrame.PresentationCount);
            }
        }

        public void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
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

            if (performConvertion)
            {
                frameData.UpdateAndConvert(this.videoConverter, videoFrame);
            }
            else
            {
                frameData.UpdateAndCopy(videoFrame);
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);

            this.frameQueue.Enqueue(frameData);
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
