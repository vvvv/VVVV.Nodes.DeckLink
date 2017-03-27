using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeralTic.DX11.Resources;

namespace VVVV.DeckLink.Direct3D11
{
    /// <summary>
    /// Simple tuple class containing both cpu and gpu frame, does not own resources
    /// </summary>
    public class DecklinkTextureFrameData
    {
        public readonly DecklinkFrameData RawFrame;
        public readonly DX11Texture2D Texture;

        public DecklinkTextureFrameData(DecklinkFrameData frame, DX11Texture2D texture)
        {
            this.RawFrame = frame;
            this.Texture = texture;
        }

        public void DisposeTexture()
        {
            Texture.Resource.Dispose();
            Texture.SRV.Dispose();
        }
    }
}
