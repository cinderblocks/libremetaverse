```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Rendering.Simple

A stand-in renderer. It implements the same `IRendering` interface as `LibreMetaverse.Rendering.MeshFoundry`, but every prim, sculpt and mesh comes out as the same unit cube. It is for tests and for code that needs *an* `IRendering` without caring about the shape; it does not try to draw a prim.

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
      ├── ModelFormats · Rendering.MeshFoundry · [Rendering.Simple]
      │
  StructuredData · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

`SimpleRenderer`, an `IRendering` registered under the name "Simple Cube Renderer". `GenerateSimpleMesh` and `GenerateFacetedMesh` (and their sculpt versions) return a cube of eight vertices and twelve triangles, centered on the origin and one unit on a side, with the prim's default texture on its one face. `TransformTexCoords` does nothing.

## Example

```csharp
using LibreMetaverse.Rendering;

IRendering renderer = new SimpleRenderer();
SimpleMesh mesh = renderer.GenerateSimpleMesh(prim, DetailLevel.Medium)!;
Console.WriteLine($"{mesh.Vertices.Count} vertices, {mesh.Indices.Count / 3} triangles");   // 8 and 12, always
```

## Use it when

You are testing code that takes an `IRendering`, or stubbing one out while you build something else. For real geometry, use `LibreMetaverse.Rendering.MeshFoundry`.

## Depends on

`LibreMetaverse.Assets` and `LibreMetaverse.Types`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Rendering.Simple
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
