```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.PrimMesher

Turns the numbers that describe a Second Life prim into triangles. Give it a shape (the number of sides, the cut, the hollow, the twist, the taper, the path) and it builds the mesh; give it a sculpt map and it builds the sculpt. It is the geometry engine behind the renderer packages, and can be used directly.

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

  on their own: [PrimMesher] · Imaging.Skia · RLV · LslTools
```

This package sits beside the stack rather than in it: it needs only `LibreMetaverse.Imaging.Abstractions`.

## What is in it

- **`PrimMesh`**: the parametric prims. Box, cylinder, prism, sphere, torus, tube and ring all come from one set of parameters: sides, profile cut, hollow, twist, taper, top shear, skew, radius and revolutions. Call `Extrude` with a path type and read `coords`, `faces` and `normals`; in viewer mode (`viewerMode = true`) it also gives per-face vertices and texture coordinates (`viewerFaces`).
- **`SculptMesh`** and **`SculptMap`**: a mesh from a sculpt texture, with the sphere, torus, plane and cylinder stitching types.
- **`ObjMesh`**: reads a Wavefront OBJ file into per-face polygons and vertices.
- The small value types the above are made of: `Coord`, `Quat`, `UVCoord`, `Face`, `ViewerFace`, `ViewerVertex`, `Path`, `Profile`.

## Example

```csharp
using LibreMetaverse.PrimMesher;

var box = new PrimMesh(sides: 4, profileStart: 0f, profileEnd: 1f, hollow: 0f, hollowSides: 4);
box.Extrude(PathType.Linear);
Console.WriteLine($"{box.coords.Count} vertices, {box.faces.Count} triangles");
```

## Use it when

You need geometry for prims without a rendering stack: an exporter, a collision shape, a physics bake, a thumbnail. If you have a `Primitive` from the world and want a ready mesh, `LibreMetaverse.Rendering.MeshFoundry` wraps this for you and also decodes mesh assets.

## Depends on

`LibreMetaverse.Imaging.Abstractions` (sculpt maps are images). It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.PrimMesher
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
