using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink
{
    /// <summary>
    /// Pool of decklink frames
    /// </summary>
    public class FramePool
    {
        private List<DecklinkFrameData> framePool = new List<DecklinkFrameData>();
        private Stack<DecklinkFrameData> availableFrames;
        private readonly int initialSize;
        private int id;

        /// <summary>
        /// Current pool size
        /// </summary>
        public int PoolSize
        {
            get { return this.framePool.Count; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="initialSize">Frame pool initial size</param>
        public FramePool(int initialSize)
        {
            this.initialSize = initialSize;
            this.availableFrames = new Stack<DecklinkFrameData>();
            for (int i = 0; i < initialSize; i++)
            {
                DecklinkFrameData frame = new DecklinkFrameData();
                this.availableFrames.Push(frame);
                this.framePool.Add(frame);
            }
        }

        /// <summary>
        /// Acquires a new frame, allocates a new one if pool is full
        /// </summary>
        /// <returns></returns>
        public DecklinkFrameData Acquire()
        {
            this.id++;
            if (this.availableFrames.Count > 0)
            {
                DecklinkFrameData frame = this.availableFrames.Pop();
                frame.id = this.id;
                return frame;
            }
            else
            {
                DecklinkFrameData frame = new DecklinkFrameData();
                this.framePool.Add(frame);
                frame.id = this.id;
                return frame;
            }
        }

        /// <summary>
        /// Recycles a frame (if contained by this pool), so it can be used again.
        /// If frame is not part of the pool, do nothing
        /// </summary>
        /// <param name="frame">Frame to recycle</param>
        public void Recycle(DecklinkFrameData frame)
        {
            //Only allow push if part of the pool
            if (this.framePool.Contains(frame))
            {
                this.availableFrames.Push(frame);
            }
        }

        /// <summary>
        /// Compact the pool (resizes to initial pool size if possible)
        /// </summary>
        /// <param name="current">Current presentation frame, which must be ignored</param>
        public void Compact(DecklinkFrameData current)
        {
            this.availableFrames.Clear();

            List<DecklinkFrameData> newList = new List<DecklinkFrameData>();

            int preserveCount = this.initialSize;
            if (current != null)
            {
                newList.Add(current);
                preserveCount--;
                this.framePool.Remove(current);
            }

            for (int i = 0; i < preserveCount; i++)
            {
                this.availableFrames.Push(this.framePool[0]);
                newList.Add(this.framePool[0]);
                this.framePool.RemoveAt(0);
            }

            for (int i = 0; i < this.framePool.Count; i++)
            {
                this.framePool[i].Dispose();
            }
            this.framePool = newList;
        }

        /// <summary>
        /// Dispose all resources from this pool
        /// </summary>
        public void Dispose()
        {
            for (var i = 0; i < this.framePool.Count; i++)
            {
                this.framePool[i].Dispose();
            }
            this.framePool.Clear();
            this.availableFrames.Clear();

        }
    }
}
