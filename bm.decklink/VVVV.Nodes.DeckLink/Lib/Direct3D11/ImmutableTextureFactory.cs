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
    public class ImmutableTextureFactory
    {
        public static DX11Texture2D CreateConvertedFrame(DX11RenderContext context, RawFrameData rawFrame)
        {
            Texture2DDescription textureDesc = new Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = SlimDX.DXGI.Format.B8G8R8A8_UNorm,
                Height = rawFrame.Height,
                Width = rawFrame.Width,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
            };
            DataRectangle slice = new DataRectangle(rawFrame.Width * 4, new DataStream(rawFrame.DataPointer, rawFrame.DataLength, true, false));
            Texture2D frameTexture = new Texture2D(context.Device, textureDesc, slice);
            ShaderResourceView frameTextureView = new ShaderResourceView(context.Device, frameTexture);
            return DX11Texture2D.FromTextureAndSRV(context, frameTexture, frameTextureView); ;
        }

        public static DX11Texture2D CreateRawFrame(DX11RenderContext context, RawFrameData rawFrame)
        {
            Texture2DDescription textureDesc = new Texture2DDescription()
            {
                ArraySize = 1,
                BindFlags = BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                Format = SlimDX.DXGI.Format.R8G8B8A8_UNorm,
                Height = rawFrame.Height,
                Width = rawFrame.Width / 2,
                MipLevels = 1,
                OptionFlags = ResourceOptionFlags.None,
                SampleDescription = new SlimDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Immutable,
            };
            DataRectangle slice = new DataRectangle(rawFrame.Width * 2, new DataStream(rawFrame.DataPointer, rawFrame.DataLength, true, false));
            Texture2D frameTexture = new Texture2D(context.Device, textureDesc, slice);
            ShaderResourceView frameTextureView = new ShaderResourceView(context.Device, frameTexture);
            return DX11Texture2D.FromTextureAndSRV(context, frameTexture, frameTextureView); ;
        }
    }
}
