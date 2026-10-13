```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Rendering.MeshFoundry

The full renderer: it turns Second Life prims, sculpts and mesh assets into renderable geometry, face by face, with the normals, skin weights and texture coordinates a modern renderer expects. It implements the `IRendering` interface from `LibreMetaverse.Assets`, so you can swap it for another renderer without touching the rest of your code.

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
      ├── ModelFormats · [Rendering.MeshFoundry] · Rendering.Simple
      │
  StructuredData · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

`MeshFoundry`, an `IRendering`:

- `GenerateFacetedMesh` and `GenerateFacetedSculptMesh` build a mesh split by face, ready to texture, from a prim or a sculpt.
- `GenerateFacetedMeshMesh` builds one from the bytes of a mesh asset, decoding the viewer's normals, the skin weights and joint data of rigged meshes, and the second texture-coordinate set and normalized scale that PBR materials use.
- `GenerateSimpleMesh` builds the overall shape with no material split, with or without normals (`GenerateSimpleMeshWithNormals`).
- `MeshSubMeshAsConvexHulls` reads the physics hulls out of a mesh asset, and `TerrainMesh` makes a mesh from a heightfield.
- `TransformTexCoords` applies a face's texture repeat, offset and rotation.

## Example

```csharp
using LibreMetaverse.Rendering;

IRendering renderer = new MeshFoundry();
FacetedMesh? mesh = renderer.GenerateFacetedMesh(prim, DetailLevel.High);
Console.WriteLine($"{mesh?.Faces.Count} faces");
```

## Use it when

You are drawing or exporting objects and want correct geometry, rigged meshes and PBR coordinates. For prim geometry without the `Primitive` and asset layers, use `LibreMetaverse.PrimMesher` directly. `LibreMetaverse.Rendering.Simple` is only a stand-in.

## Depends on

`LibreMetaverse.Assets`, `LibreMetaverse.StructuredData`, `LibreMetaverse.Types`, `LibreMetaverse.Imaging.Abstractions` and `LibreMetaverse.PrimMesher`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Rendering.MeshFoundry
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
