# DeckLink for vvvv beta x64

## Credits
Sourcecode generously provided as-is by [NSYNK](https://nsynk.de).

## Usage

## Build from source

1. Install `Desktop Video 12.1` from: [blackmagicdesign.com/developer](https://www.blackmagicdesign.com/de/developer/)
2. Open `VVVV.Nodes.BlackMagic.sln` and build the project

## Deploy
The solution will build the `.dll` file right into the `Patches/` folder. 

## HDMI Input
If a black picture is experienced, you may have to change the pixel format on the input format. This happens mostly with Nvidia cards providing the input signal. You can change the pixel color output format in the Nvidia control panel:

![Nvidia control panel](README/nvidia-control-panel.png)

## Helpful Links
- [DeckLinkCapabilities: A Printout of Capabilities of Blackmagic Design/DeckLink Hardware](http://alax.info/blog/1454)
- [Correcting HDMI Colour on Nvidia and AMD GPUs](https://pcmonitors.info/articles/correcting-hdmi-colour-on-nvidia-and-amd-gpus/)
