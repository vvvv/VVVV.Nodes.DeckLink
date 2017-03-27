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
    public class YuvToRGBConverter : IDX11Resource
    {
        private DX11RenderContext context;
        private DX11ShaderInstance shader;

        private EffectResourceVariable inputTexture;
        private EffectScalarVariable halfWidth;


        public YuvToRGBConverter(DX11RenderContext context)
        {
            this.context = context;
            this.shader = ShaderUtils.GetShader(context, "YUV2RGB");
            this.inputTexture = this.shader.Effect.GetVariableByName("InputTexture").AsResource();
            this.halfWidth = this.shader.Effect.GetVariableByName("CompressedWidth").AsScalar();
        }

        public void Apply(DX11Texture2D inputTexture, DX11RenderTarget2D outputTexture)
        {
            int uncompressedWidth = inputTexture.Width * 2;

            context.RenderTargetStack.Push(outputTexture);

            context.Primitives.ApplyFullTriVS();

            this.inputTexture.SetResource(inputTexture.SRV);
            this.halfWidth.Set(inputTexture.Width);
            this.shader.ApplyPass(0);

            context.CurrentDeviceContext.Draw(3, 0);

            context.RenderTargetStack.Pop();
        }

        public void Dispose()
        {
            if (this.shader != null)
            {
                this.shader.Dispose();
                this.shader = null;
            }
        }
    }
}
