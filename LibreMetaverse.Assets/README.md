```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Assets

Everything LibreMetaverse knows about the *things* in a virtual world, without a connection to one. Assets, primitives and their textures, inventory items, the avatar's skeleton and appearance, the data behind a mesh upload, glTF documents, animations and logging. You can read a notecard, decode a mesh, build a prim or check a model for upload from a program that never logs in.

```
  Archiving · Utilities · Voice.Vivox · Voice.WebRTC
      │  built on the client
  LibreMetaverse   the client: login, managers, inventory
      │
  Transport        UDP socket, reliable circuit, throttle
      │
  Protocol         packets and capability messages
      │
 [Assets]          the offline data model
      ├── ModelFormats · Rendering.MeshFoundry · Rendering.Simple
      │
  StructuredData · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

- **Asset types**: `AssetTexture`, `AssetMesh`, `AssetNotecard`, `AssetPrim`, `AssetSound`, `AssetAnimation`, `AssetClothing`, `AssetBodypart`, `AssetGesture`, `AssetLandmark`, `AssetScriptText`, `AssetScriptBinary`, `AssetMaterial`, `AssetSettings` and the others. Each encodes to and decodes from the bytes a grid stores. `SceneObjectGroupXmlReader` and `SceneObjectGroupXmlWriter` read and write OpenSim's linkset XML.
- **Primitives**: `Primitive` with its construction data, `TextureEntry`, particle systems, flexible and light parameters, sculpt and mesh data, and `LegacyMaterial`.
- **Inventory**: the item classes (`InventoryItem`, `InventoryFolder`, `InventoryNotecard` and the rest), `Permissions` and `InventoryHelpers`.
- **Avatar data**: `LindenSkeleton`, the visual parameters, the tree and grass definitions, `BVHDecoder`, `AnimationTrack` and `AnimeshPlayer`.
- **Models**: `ModelPrim` and `ModelUploadChecks`, the level-of-detail and physics-shape generators, `IModelLoader` (implemented by `LibreMetaverse.ModelFormats`), `GltfDocument`, and the `IRendering` interface the renderer packages implement.
- **Sounds**: the table of the grid's standard UI sounds (`Sounds`).
- **Logging**: `Logger`, `ILogContext` and `ProcessSettings`, built on `Microsoft.Extensions.Logging`.

## Example

```csharp
using LibreMetaverse;
using LibreMetaverse.Assets;

var note = new AssetNotecard { BodyText = "Meet at the fountain." };
note.Encode();                       // note.AssetData now holds what a grid would store
byte[] stored = note.AssetData;

var read = new AssetNotecard(UUID.Random(), stored);
Console.WriteLine(read.Decode() ? read.BodyText : "unreadable");
```

## Use it when

You are building an asset converter, a content pipeline, a viewer-side tool, or the storage half of a server: anything that handles grid content without a login. It is also what the other packages (`Protocol`, `ModelFormats`, the renderers) are written against.

## Depends on

`LibreMetaverse.Types`, `LibreMetaverse.StructuredData` and `LibreMetaverse.Imaging.Abstractions`, plus CoreJ2K (JPEG 2000), Pfim, OggVorbisEncoder, MessagePack, zlib and ZLogger. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Assets
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
