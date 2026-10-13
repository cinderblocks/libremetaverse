```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Archiving

OpenSim region archives. An OAR file is a gzipped tar holding a region's objects, terrain, parcels, settings and the assets they use; this package unpacks them and packs them, and reads and writes the tar format underneath. It is how you move a whole region between OpenSim installations, or take one apart to look at it.

```
  [Archiving] · Utilities · Voice.Vivox · Voice.WebRTC
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

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

- **`OarFile`**: `UnpackageArchive` reads an archive and calls you back for each asset, for the terrain, for each linkset (an `AssetPrim`) and for the region settings. `PackageArchive` writes one from a directory. `SaveTerrain`, `SaveParcels`, `SaveRegionSettings`, `SavePrimsAsync` and `SaveAssetsAsync` fill that directory from a simulator you are connected to.
- **`ArchiveInfo`**: the archive's `archive.xml` control file. It records the format version, when it was made, the region size (so a megaregion round-trips) and whether assets are included. Archives are written as version 0.8, the layout OpenSim has written since 2012. Older archives, which have no control file or an older one, still load, and one from a newer OpenSim than the library knows is read as 0.8 and flagged through `IsNewerThanSupported`.
- **`TarArchiveReader`** and **`TarArchiveWriter`**: plain tar entry by entry, for archives that are not OARs.
- **`RegionSettings`** and **`ArchiveConstants`**: the settings structure and the paths and file-name conventions inside an archive.

## Example

```csharp
using LibreMetaverse.Assets;

ArchiveInfo? info = OarFile.ReadArchiveInfo("region.oar");
Console.WriteLine(info == null ? "no archive.xml" : $"version {info.MajorVersion}.{info.MinorVersion}, {info.SizeX}x{info.SizeY}");

OarFile.UnpackageArchive("region.oar",
    (asset, read, total) => Console.WriteLine($"{asset.AssetType} {asset.AssetID}"),
    (terrain, read, total) => Console.WriteLine($"terrain {terrain.GetLength(0)}x{terrain.GetLength(1)}"),
    (linkset, read, total) => Console.WriteLine($"{linkset.Parent?.Name}"),
    (name, settings) => Console.WriteLine($"settings of {name}"));
```

Reading needs no connection. Saving a region from a live simulator uses the `GridClient` of the `LibreMetaverse` package.

## Use it when

You are building region backup, restore or conversion tools, inspecting an archive from the web, or preparing content for an OpenSim region. The archive types are in the `LibreMetaverse.Assets` namespace, as they were when they were part of the main package.

## Depends on

`LibreMetaverse` (for saving from a simulator) and `LibreMetaverse.Types`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Archiving
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
