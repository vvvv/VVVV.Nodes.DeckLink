using FeralTic.DX11;
using FeralTic.DX11.Resources;
using SlimDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VVVV.DeckLink.Direct3D11
{
    public class YuvToRGBConverterWithTarget : IDX11Resource
    {
        private readonly DX11RenderContext context;
        private readonly YuvToRGBConverter converter;
        private DX11RenderTarget2D renderTarget;

        public YuvToRGBConverterWithTarget(DX11RenderContext context, YuvToRGBConverter converter)
        {
            this.context = context;
            this.converter = converter;
        }

        public DX11Texture2D Apply(DX11Texture2D inputTexture)
        {
            int uncompressedWidth = inputTexture.Width * 2;
            if (renderTarget != null)
            {
                if (renderTarget.Width != uncompressedWidth || renderTarget.Height != inputTexture.Height)
                {
                    renderTarget.Dispose();
                    renderTarget = null;
                }
            }

            if (renderTarget == null)
            {
                renderTarget = new DX11RenderTarget2D(this.context, uncompressedWidth, inputTexture.Height, new SlimDX.DXGI.SampleDescription(1, 0), SlimDX.DXGI.Format.R8G8B8A8_UNorm);
            }
            context.RenderTargetStack.Push(renderTarget);
            this.converter.Apply(inputTexture, renderTarget);
            context.RenderTargetStack.Pop();
            return this.renderTarget;
        }

        public void Dispose()
        {
            if (this.renderTarget != null)
            {
                this.renderTarget.Dispose();
                this.renderTarget = null;
            }
        }
    }
}
