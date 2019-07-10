# VVVV Decklink

## Setup
:warning: Mind that you might need the latest BlackMagic Driver (11.2)

1. Obtain the DeckLink SDK
    A. Download the BlackMagic SDK from
    [blackmagicdesign.com/developer](https://www.blackmagicdesign.com/de/developer/)
    (*Developer SDK/Desktop Video SDK* under **Latest Downloads**)
    B. Get it from the BOX: `Current\Ongoing\1904_SoftwarePack\SDKs`
2. Unzip the SDK package into the root of `bm.decklink`
3. Open `VVVV.Nodes.BlackMagic.sln` and build the project

## Deploy
The solution will build the `.dll` files right into the `Patches/` folder. Use
`release.bat vvvv.nodes.blackmagic` to copy all the files into the `Release/`
dir for usage with the vvvv packs dir.
