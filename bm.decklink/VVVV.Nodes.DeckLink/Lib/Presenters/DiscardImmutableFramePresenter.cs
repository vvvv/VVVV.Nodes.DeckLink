using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DeckLinkAPI;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using VVVV.DeckLink.Direct3D11;

namespace VVVV.DeckLink.Presenters
{
    /// <summary>
    /// Discard mode frame presentation, always present last frame, regardless of what happened.
    /// Creates an immutable texture in background, and takes care of disposing old frames (this is done during update)
    /// </summary> 
    public class DiscardImmutableFramePresenter : IDecklinkFramePresenter, IDisposable, IDiscardCounter
    {
        private bool isDisposed = false;
        private readonly DX11RenderContext renderContext;
        private readonly DecklinkVideoFrameConverter videoConverter;
        private readonly int maxQueueSize;

        private DecklinkFrameData frame = new DecklinkFrameData();
        private DX11Texture2D currentTexture;

        private bool isNewFrame = false;
        private bool lastFramePresented = false;
        private int discardCount = 0;

        private object syncRoot = new object();
        private Queue<DX11Texture2D> pendingFramesForDeletion = new Queue<DX11Texture2D>();

        public int QueueSize
        {
            get { return pendingFramesForDeletion.Count; }
        }

        public int DiscardCount
        {
            get { return this.discardCount; }
        }

        public DiscardImmutableFramePresenter(DX11RenderContext renderContext, DecklinkVideoFrameConverter videoConverter, int maxQueueSize)
        {
            if (renderContext == null)
                throw new ArgumentNullException("renderContext");
            if (videoConverter == null)
                throw new ArgumentNullException("videoConverter");

            this.videoConverter = videoConverter;
            this.renderContext = renderContext;
            this.maxQueueSize = maxQueueSize;
        }

        public FrameDataResult GetPresentationFrame()
        {
            this.FlushQueue();

            var result = FrameDataResult.Texture2D(this.currentTexture, isNewFrame, 1);
            this.isNewFrame = false;
            this.lastFramePresented = true;
            return result;
        }

        public void PushFrame(IDeckLinkVideoInputFrame videoFrame, bool performConvertion)
        {
            if (this.renderContext.Device.Disposed)
                return;

            //If queue is bigger than max size, we do nothing and also discard, as it means that mainloop is likely stuck
            if (this.pendingFramesForDeletion.Count >= maxQueueSize)
            {
                this.discardCount++;
                return;
            }

            if (this.lastFramePresented == false)
            {
                this.discardCount++;
            }

            DX11Texture2D newTexture;

            if (performConvertion)
            {
                this.frame.UpdateAndConvert(this.videoConverter, videoFrame);
                newTexture = ImmutableTextureFactory.CreateConvertedFrame(this.renderContext, this.frame.ConvertedFrameData);
            }
            else
            {
                this.frame.UpdateAndCopy(videoFrame);
                newTexture = ImmutableTextureFactory.CreateRawFrame(this.renderContext, this.frame.RawFrameData);
            }
            System.Runtime.InteropServices.Marshal.ReleaseComObject(videoFrame);

            //Push current texture to dispose, since it can still be used somewhere in the patch for presentation, we flush queue during update
            lock (syncRoot)
            {
                if (this.currentTexture != null)
                {
                    this.pendingFramesForDeletion.Enqueue(this.currentTexture);
                }
                this.currentTexture = newTexture;
            }

            this.isNewFrame = true;
            this.lastFramePresented = false;
        }

        public void Dispose()
        {
            this.isDisposed = true;
            //Clear queue
            lock (syncRoot)
            {
                if (this.frame != null)
                {
                    this.frame.Dispose();
                    this.frame = null;
                }
            }
            this.FlushQueue();
        }

        private void FlushQueue()
        {
            //Clear queue
            lock (syncRoot)
            {
                while (this.pendingFramesForDeletion.Count > 0)
                {
                    var texture = this.pendingFramesForDeletion.Dequeue();
                    texture.Resource.Dispose();
                    texture.SRV.Dispose();
                }
            }
        }

        public void Reset()
        {
            this.discardCount = 0;
        }
    }
}
