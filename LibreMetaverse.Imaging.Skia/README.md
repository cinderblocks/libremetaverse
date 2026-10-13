```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Imaging.Skia

A working image codec for LibreMetaverse, built on SkiaSharp. It reads the everyday picture formats and gives back the library's own `ManagedImage`, so the model loaders and the prim mesher can use textures that come as PNG, JPEG or BMP files.

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
  StructuredData · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · [Imaging.Skia] · RLV · LslTools
```

This package sits beside the stack rather than in it: it needs only `LibreMetaverse.Imaging.Abstractions`.

## What is in it

`SkiaTextureCodec`, an `ITextureCodec`. Decode a stream, get a `ManagedImage` with its red, green, blue and alpha channels filled in. `SkiaTextureCodec.ToManagedImage` converts a SkiaSharp bitmap you already have.

## Example

```csharp
using LibreMetaverse.Imaging;
using LibreMetaverse.Imaging.Skia;

ITextureCodec codec = new SkiaTextureCodec();
using FileStream file = File.OpenRead("texture.png");
ManagedImage image = codec.Decode(file);
Console.WriteLine($"{image.Width}x{image.Height} {image.Channels}");
```

Hand the same codec to `new GltfLoader(codec)` or `new ColladaLoader(codec)` to have their textures decoded.

## Use it when

You want textures to work in the model loaders without writing a codec. Skia brings native libraries with it; if that is unwelcome (a trimmed or AOT build, a platform Skia does not cover), implement `ITextureCodec` over the library you prefer instead. The interface is one method.

## Depends on

`LibreMetaverse.Imaging.Abstractions` and SkiaSharp. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Imaging.Skia
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
