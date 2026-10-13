```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Imaging.Abstractions

The image type and the one-method codec interface that let LibreMetaverse work with pictures without choosing an imaging library for you. Texture code decodes through `ITextureCodec`; you decide what sits behind it.

```
  Archiving · Utilities · Voice.Vivox · Voice.WebRTC
      │  built on the client
  LibreMetaverse   the client: login, managers, inventory
      │
  Transport        UDP socket, reliable circuit, throttle
      │
  Protocol         packets and capability messages
      │
  Assets           the offline data model
      ├── ModelFormats · Rendering.MeshFoundry · Rendering.Simple
      │
  StructuredData · [Imaging.Abstractions]
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

- `ManagedImage`: a picture held as separate red, green, blue, alpha and bump channels, the shape Second Life's textures and bakes have. It can convert between channel layouts, resize (nearest neighbor or bilinear), clone and export raw bytes.
- `ITextureCodec`: `ManagedImage Decode(Stream stream)`, which turns the bytes of a PNG, JPEG, BMP or whatever your codec reads into a `ManagedImage`.

Second Life's own JPEG 2000 textures are decoded by `LibreMetaverse.Assets`; the codec is for the other formats that arrive with a model or an upload.

## Plugging one in

`LibreMetaverse.Imaging.Skia` is a ready-made codec built on SkiaSharp. To use something else, implement the interface:

```csharp
using LibreMetaverse.Imaging;

public sealed class FlatGreyCodec : ITextureCodec
{
    public ManagedImage Decode(Stream stream)
    {
        // A real codec reads the stream with the imaging library of your choice.
        // This one ignores it and returns a 2 x 2 grey picture.
        var image = new ManagedImage(2, 2, ManagedImage.ImageChannels.Color);
        Array.Fill(image.Red, (byte)128);
        Array.Fill(image.Green, (byte)128);
        Array.Fill(image.Blue, (byte)128);
        return image;
    }
}
```

## Use it when

You are writing code that needs to *accept* an image decoder (a loader, a renderer, an exporter) and do not want to pin your callers to one. If you only need to decode, reference `LibreMetaverse.Imaging.Skia` instead.

## Depends on

.NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10, with no dependencies.

## Install

```
dotnet add package LibreMetaverse.Imaging.Abstractions
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
