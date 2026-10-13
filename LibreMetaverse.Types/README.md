```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Types

The basic types every other LibreMetaverse package is written in: the identifier, vector, quaternion and color types of the virtual-world protocol, the enums that name the things in it, and a small kit of caches and locks. It is the bottom of the stack and has no dependencies of its own.

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
 [Types]           UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

- **Values**: `UUID`, `Vector2`, `Vector3`, `Vector3d`, `Vector4`, `Quaternion`, `Matrix4`, `Color4`, `Ray`, `CoordinateFrame`.
- **Names for protocol values**: `AssetType`, `InventoryType`, `FolderType`, `WearableType`, `AttachmentPoint`, `PCode`, `PrimFlags`, `PrimType`, `SculptType`, `PathCurve`, `ProfileCurve`, `FaceType`, `SaleType`, `ClickAction`, `PhysicsShapeType` and the other enums a viewer and a simulator share.
- **Helpers**: `Utils` (conversions, byte packing, the Unix epoch), `CRC32`, and `SlurlParser`, which reads and builds `secondlife://` location links and `/app/` commands.
- **Caches**: `CacheDictionary` with LRU or MRU removal, `ExpiringCache`, `DoubleDictionary`, `MultiValueDictionary`, `ObservableDictionary`, and `TokenBucket` for rate limits.
- **Threading** (`LibreMetaverse.Threading`): optimistic and spin reader/writer locks, managed events and semaphores, and `WaitHandleAsyncFactory` for awaiting wait handles.

## Example

```csharp
using LibreMetaverse;

UUID id = UUID.Random();
Vector3 position = new Vector3(128, 128, 25);
Quaternion turn = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(Math.PI / 2));
Vector3 turned = position * turn;   // a quarter turn about the vertical axis

var slurl = new SlurlParser("secondlife://Ahern/128/64/25");
Console.WriteLine($"{slurl.Sim} at {slurl.X}, {slurl.Y}, {slurl.Z}");
```

## Use it when

You are writing a tool that touches viewer data (a log analyzer, an asset converter, a map tool) and wants `UUID` and `Vector3` without taking a networking library with them. If you also need to read or write LLSD, add `LibreMetaverse.StructuredData`; for the asset formats, `LibreMetaverse.Assets`.

## Depends on

.NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10. On .NET Framework it uses `System.Buffers` and `System.Memory`; nowhere else does it depend on anything.

## Install

```
dotnet add package LibreMetaverse.Types
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
