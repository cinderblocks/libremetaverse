```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.ModelFormats

3D model files in, Second Life models out, and the other way round. It loads Collada, glTF 2.0 and Wavefront OBJ into the `ModelPrim` structures that mesh upload uses, keeping rigging and materials, and it exports meshes you have downloaded, rigged ones included, as glTF, OBJ or Collada.

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
      ├── [ModelFormats] · Rendering.MeshFoundry · Rendering.Simple
      │
  StructuredData · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

- **Loaders**, all behind the `IModelLoader` interface (and `IDiagnosticModelLoader`, which adds a list of what it found wrong or changed):
  - `ColladaLoader` for `.dae`, with skins, joint weights and materials.
  - `GltfLoader` for `.gltf` and `.glb`, including skins and, if you ask (`LoadPbrTextures`), the PBR textures.
  - `ObjLoader` for `.obj`, with the color, opacity and diffuse texture of its `.mtl` materials.
- **Exporters**: `GltfExporter` (a `GltfDocument`, or a binary `.glb` with `ToGlb()`), `ObjExporter` and `ColladaExporter` (each gives you the files to write, as `ExportedFile`s). Give them an `ImageProvider` to include textures.
- **Collada schema classes**: the COLLADA object model (the 1.4.1 schema), for when you need to read or write the document yourself.
- **Limits you can change**: loaders read textures only from beside the model unless you turn off `RestrictTexturesToModelDirectory`, refuse files over `MaxFileSize` (256 MiB) and meshes over `MaxVertices` (4 million), and cap the number of joints at what a rigged mesh may have.

## Example

```csharp
using LibreMetaverse.Imaging.Skia;
using LibreMetaverse.ImportExport;

var loader = new GltfLoader(new SkiaTextureCodec());
ModelLoadResult result = loader.LoadWithDiagnostics("chair.glb", loadImages: true);

foreach (ModelDiagnostic note in result.Diagnostics)
    Console.WriteLine(note);
Console.WriteLine($"{result.Prims.Count} objects ready to upload");
```

The loaders take an optional `ITextureCodec` to decode the pictures that come with a model; `LibreMetaverse.Imaging.Skia` provides one.

To upload what you loaded, pass the `ModelPrim`s to `client.Inventory.UploadModelAsync` in the `LibreMetaverse` package, which checks the model against the grid's rules first.

## Use it when

You are importing models into Second Life or OpenSim, converting between model formats, or exporting what you can see in the world. The data model (`ModelPrim`, the upload checks, the level-of-detail generators) is in `LibreMetaverse.Assets`, so a tool that only validates or converts models can use this package without the client.

## Depends on

`LibreMetaverse.Assets` and `LibreMetaverse.Types`, and CoreJ2K. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.ModelFormats
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
