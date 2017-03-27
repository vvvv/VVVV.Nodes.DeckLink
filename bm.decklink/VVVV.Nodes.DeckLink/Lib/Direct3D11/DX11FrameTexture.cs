using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeralTic.DX11;
using FeralTic.DX11.Resources;
using SlimDX;
using SlimDX.Direct3D11;

namespace VVVV.DeckLink.Direct3D11
{
    public class DX11FrameTexture : IDisposable
    {
        private YuvToRGBConverter converter;
        private DX11Texture2D uploadTexture;
        private DX11RenderTarget2D convertedTexture;

        private bool needConvertion;
        private bool isConverted = false;
        private int presentCount;

        private DX11FrameTexture()
        {
            this.presentCount = 0;
            this.needConvertion = false;
        }

        public DX11Texture2D AcquireForPresentation()
        {
            if (this.needConvertion && this.isConverted == false)
            {
                this.converter.Apply(this.uploadTexture, this.convertedTexture);
                this.isConverted = true;
            }

            if (this.needConvertion)
            {
                return this.convertedTexture;
            }
            else
            {
                return this.uploadTexture;
            }
        }

        public int PresentCount
        {
            get { return this.presentCount; }
        }

        public void MarkPresent()
        {
            this.presentCount++;
        }

        /// <summary>
        /// Creates a directly converted frame
        /// </summary>
        /// <param name="context"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static DX11FrameTexture CreateConverted(DX11RenderContext context, int width, int height, IntPtr dataPointer, int dataSize)
        {
            Texture2DDescription textureDesc = new Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
                Height = width,
                Width = height,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
            };

            DataRectangle slice = new DataRectangle(width*4, new DataStream(dataPointer, dataSize, true, false));

            Texture2D videoTexture = new Texture2D(context.Device, textureDesc, slice);
            ShaderResourceView videoView = new ShaderResourceView(context.Device, videoTexture);

            DX11FrameTexture f = new DX11FrameTexture();
            f.uploadTexture = DX11Texture2D.FromTextureAndSRV(context, videoTexture, videoView);

            return f;
        }

        /// <summary>
        /// Creates a directly converted frame
        /// </summary>
        /// <param name="context"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static DX11FrameTexture CreateRaw(DX11RenderContext context, int width, int height, IntPtr dataPointer, int dataSize)
        {
            Texture2DDescription textureDesc = new Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
                Height = width,
                Width = height,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
            };

            DataRectangle slice = new DataRectangle(width * 2, new DataStream(dataPointer, dataSize, true, false));

            Texture2D videoTexture = new Texture2D(context.Device, textureDesc, slice);
            ShaderResourceView videoView = new ShaderResourceView(context.Device, videoTexture);

            DX11FrameTexture f = new DX11FrameTexture();
            f.uploadTexture = DX11Texture2D.FromTextureAndSRV(context, videoTexture, videoView);

            return f;
        }

        /// <summary>
        /// Creates raw frame with a backend rendertarget for convertion purposes
        /// </summary>
        /// <param name="context"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static DX11FrameTexture CreateRawWithConversion(DX11RenderContext context, YuvToRGBConverter converter, int width, int height, IntPtr dataPointer, int dataSize)
        {
            Texture2DDescription textureDesc = new Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
                Height = width,
                Width = height,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
            };

            DataRectangle slice = new DataRectangle(width * 2, new DataStream(dataPointer, dataSize, true, false));

            Texture2D videoTexture = new Texture2D(context.Device, textureDesc, slice);
            ShaderResourceView videoView = new ShaderResourceView(context.Device, videoTexture);

            DX11FrameTexture f = new DX11FrameTexture();
            f.uploadTexture = DX11Texture2D.FromTextureAndSRV(context, videoTexture, videoView);
            f.needConvertion = true;
            f.convertedTexture = new DX11RenderTarget2D(context, width, height, new SlimDX.DXGI.SampleDescription(1, 0),
                 SlimDX.DXGI.Format.R8G8B8A8_UNorm);
            f.converter = converter;

            return f;
        }

        public void Dispose()
        {
            if (this.uploadTexture != null)
            {
                this.uploadTexture.Dispose();
                this.uploadTexture = null;
            }
            if (this.convertedTexture != null)
            {
                this.convertedTexture.Dispose();
                this.convertedTexture = null;
            }
        }
    }
}
