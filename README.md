```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```
# LibreMetaverse

LibreMetaverse is a fork of libOpenMetaverse which in turn was a fork of
libSecondLife, a library for developing Second Life-compatible virtual world
clients. LibreMetaverse returns the focus to up-to-date Second Life and OpenSim
compatibility with an eye to performance, multi-threading, memory management, and
dependency injection support.

The canonical source for LibreMetaverse can be found at:
https://github.com/cinderblocks/libremetaverse

## Quick Start

### New to LibreMetaverse?

Check out our example applications to learn the library:

- **[SimpleBot](Programs/examples/SimpleBot/)** - Build an interactive bot that responds to IMs
- **[PrimInspector](Programs/examples/PrimInspector/)** - Inspect 3D objects and their properties
- **[InventoryExplorer](Programs/examples/InventoryExplorer/)** - Browse and export inventory data

See all examples in [`Programs/examples/`](Programs/examples/) | [Examples README](Programs/examples/README.md)

### Need Tools?

- **[OSDInspector](Programs/tools/OSDInspector/)** - Inspect, validate, and convert OSD data files

See all tools in [`Programs/tools/`](Programs/tools/) | [Tools README](Programs/tools/README.md)

## Packages

LibreMetaverse is a family of packages. Reference the one that matches what you are building; each package's page on NuGet describes it in full.

**Building a client or a bot?** Reference `LibreMetaverse`. It brings in the packages below it, and the types in them are still in the `LibreMetaverse` namespace.

| Package | For |
|---|---|
| [`LibreMetaverse`](https://www.nuget.org/packages/LibreMetaverse) | The client: log in, move, chat, build, manage inventory, groups and land. Brings in the six packages below it. |
| [`LibreMetaverse.Transport`](https://www.nuget.org/packages/LibreMetaverse.Transport) | The UDP circuit with no client: socket loop, reliable delivery, throttle. |
| [`LibreMetaverse.Protocol`](https://www.nuget.org/packages/LibreMetaverse.Protocol) | The wire protocol with no client: every UDP packet, the capability messages, the login request and response. |
| [`LibreMetaverse.Assets`](https://www.nuget.org/packages/LibreMetaverse.Assets) | The offline data model: asset types, prims and textures, inventory items, avatar data, model upload checks. |
| [`LibreMetaverse.StructuredData`](https://www.nuget.org/packages/LibreMetaverse.StructuredData) | LLSD in XML, binary, notation, JSON and protobuf. |
| [`LibreMetaverse.Imaging.Abstractions`](https://www.nuget.org/packages/LibreMetaverse.Imaging.Abstractions) | `ManagedImage` and the `ITextureCodec` interface. |
| [`LibreMetaverse.Types`](https://www.nuget.org/packages/LibreMetaverse.Types) | `UUID`, vectors, quaternions, colors, the shared enums, caches and locks. |

**Working with content or the protocol, with no login?** Reference only the package you need. `LibreMetaverse.Assets` reads and writes assets, `LibreMetaverse.Protocol` builds and parses packets, and `LibreMetaverse.StructuredData` reads LLSD, each without the client. Each package uses only the ones beneath it in the table.

**Add-ons**, which you reference alongside the above:

| Package | For |
|---|---|
| [`LibreMetaverse.ModelFormats`](https://www.nuget.org/packages/LibreMetaverse.ModelFormats) | Load Collada, glTF and OBJ models for upload; export meshes as glTF, OBJ or Collada. |
| [`LibreMetaverse.Archiving`](https://www.nuget.org/packages/LibreMetaverse.Archiving) | Read and write OpenSim region archives (OAR) and tar files. |
| [`LibreMetaverse.Utilities`](https://www.nuget.org/packages/LibreMetaverse.Utilities) | Persistent login, keeping an avatar in a region, typed-out chat. |
| [`LibreMetaverse.Voice.WebRTC`](https://www.nuget.org/packages/LibreMetaverse.Voice.WebRTC) | Voice chat on Second Life's WebRTC voice service. |
| [`LibreMetaverse.Voice.Vivox`](https://www.nuget.org/packages/LibreMetaverse.Voice.Vivox) | Voice chat through the older Vivox service and its `SLVoice` daemon. |
| [`LibreMetaverse.RLV`](https://www.nuget.org/packages/LibreMetaverse.RLV) | RestrainedLove command handling. Depends on no other LibreMetaverse package. |
| [`LibreMetaverse.Rendering.MeshFoundry`](https://www.nuget.org/packages/LibreMetaverse.Rendering.MeshFoundry) | Geometry from prims, sculpts and mesh assets, with rigging and PBR coordinates. |
| [`LibreMetaverse.Rendering.Simple`](https://www.nuget.org/packages/LibreMetaverse.Rendering.Simple) | A stand-in renderer that returns a unit cube for every prim, for tests. |
| [`LibreMetaverse.PrimMesher`](https://www.nuget.org/packages/LibreMetaverse.PrimMesher) | Triangles from prim and sculpt parameters. |
| [`LibreMetaverse.Imaging.Skia`](https://www.nuget.org/packages/LibreMetaverse.Imaging.Skia) | An `ITextureCodec` for PNG, JPEG and BMP, built on SkiaSharp. |
| [`LibreMetaverse.LslTools`](https://www.nuget.org/packages/LibreMetaverse.LslTools) | Lexer and parser toolkit for tools that read LSL. |
| [`LibreMetaverse.PacketDump`](https://www.nuget.org/packages/LibreMetaverse.PacketDump) | A command-line tool that logs the type of every packet a grid sends. |

## Requirements

- .NET SDK 8.0, 9.0, or 10.0 installed (10.0 recommended). Older SDK versions may build some projects but are not tested.
- `dotnet` CLI available on PATH. Verify with:

```
dotnet --info
```

- On Windows you can use Visual Studio 2022 or later with .NET workloads installed. On Linux/macOS use the official .NET SDK installers.

> **Upgrading?** [MIGRATION.md](MIGRATION.md) covers 3.1 to 3.2 (the package split, and the few members that changed) and 2.x to 3.0.

## Recommended quick build (CLI)

From repository root:

1. Restore packages:

```
# Restore packages for the solution
# Example: dotnet restore LibreMetaverse.sln
# Or restore and build directly from the root directory:

dotnet restore
```

2. Build the solution (Release):

```
# Build the entire repo (builds discovered projects)
# Recommended: use 'dotnet build' which runs source generators automatically

dotnet build -c Release
```

This builds projects for their configured target frameworks (`netstandard2.1`, `net481`, `net8.0`, `net9.0`, `net10.0`).
If you need to force a single framework for a specific project use `-f` on the `dotnet build` command for that project.

Notes:
- Some sample programs and tests target Windows-only frameworks (e.g. .NET Framework)
  and will produce warnings or errors on non-Windows hosts. You can ignore those when building cross-platform.
- If you prefer MSBuild directly, be aware: some projects include custom source generators under the
  `SourceGenerators` folder. When using MSBuild (or `dotnet msbuild`) you may need to build/run those
  generator projects manually before building the consumer projects. Using `dotnet build` is recommended
  because it will execute source generators as part of the normal build flow when configured correctly.

If you still want to run MSBuild directly:

```
# Build generator projects first (example):
# dotnet build SourceGenerators/PacketSourceGenerator/PacketSourceGenerator.csproj -c Release
# dotnet build SourceGenerators/VisualParamGenerator/VisualParamGenerator.csproj -c Release

# Then run msbuild on the solution (adjust solution filename as needed):
# dotnet msbuild LibreMetaverse.sln -t:Build -p:Configuration=Release
```

## Building specific projects

To build an individual project (example `TestClient`):

```
dotnet build Programs/examples/TestClient/TestClient.csproj -c Release
```

Outputs for each project are under that project's `bin/<Configuration>/<TargetFramework>/` directory
(for example `LibreMetaverse/bin/Release/net8.0/`).

## Running examples

Change into the example project's output folder and run with `dotnet` (or execute the produced
executable on platforms that produce a native runnable file):

```
cd Programs/examples/TestClient/bin/Release/net8.0/
dotnet TestClient.dll    # on platforms that require 'dotnet' to run
# or
./TestClient              # if the project produced a runnable executable on your platform
```

## Visual Studio (Windows)

- Open the solution file present in the repo root (for example `LibreMetaverse.sln`) in Visual Studio.
- Select the desired solution configuration (Debug/Release) and target framework (if applicable) and build.
- Some solution items target .NET Framework for Windows-only tests/tools — you can unload those projects if you don't need them.

## Platform-specific notes

- Windows
  - Full .NET Framework projects (e.g. `net48` test/tools) require Windows and Visual Studio/MSBuild.
    Use Visual Studio or `dotnet msbuild` from a Developer Command Prompt.
  - Executables built for Windows can be run directly (`./Program.exe`) or via `dotnet` for framework-dependent builds.
  - Some third-party SDKs (voice/Vivox, native clients) may require platform installers or SDKs — consult the
    specific project folders for details.

- Linux
  - Use the official .NET SDK packages for your distribution. `dotnet build` and `dotnet run` are the recommended workflow.
  - Some projects may reference native libraries or Windows-only APIs; those projects will fail or warn during build
    and can be ignored if not required.
  - If you use MSBuild directly, ensure source generators are available (see "MSBuild" notes above).

- macOS
  - Install the official .NET SDK (8.0/9.0). `dotnet build` / `dotnet run` are supported similarly to Linux.
  - GUI or Windows-specific projects will not run; use the example CLI apps and libraries that target cross-platform frameworks.

- CI and cross-platform builds
  - For CI prefer `dotnet build` and run a matrix across `ubuntu-latest`, `macos-latest`, and `windows-latest` if you
    need to validate cross-platform compatibility.
  - Exclude or conditionally run Windows-only projects on non-Windows runners.

- Native dependencies & source generators
  - Some components (voice, WebRTC, Vivox) may depend on native binaries or SDKs. Check the individual project
    README files for native prerequisites and installation steps.
  - If you choose to use `dotnet msbuild` directly instead of `dotnet build`, you may need to build or run
    the `SourceGenerators` projects first so generated sources are available to consumer projects.

## Tests

Some tests target Windows-only frameworks; run the cross-platform tests using the CLI where applicable. Example (where supported):

```
dotnet test LibreMetaverse.Tests/LibreMetaverse.Tests.csproj -c Release
```

The model loaders and exporters have their own project, `LibreMetaverse.ModelFormats.Tests`, and region archives have `LibreMetaverse.Archiving.Tests`, the wire protocol has `LibreMetaverse.Protocol.Tests` and the UDP transport has `LibreMetaverse.Transport.Tests`, neither of which references the client; `dotnet test LibreMetaverse.sln` runs all of them.

## Troubleshooting

- Missing Windows-only assemblies: you may see warnings/errors for projects that rely on Windows-specific APIs on Linux/macOS.
  These are expected for some example/test projects and can be ignored unless you need those projects.
- Out-of-date SDK: Ensure the SDK version reported by `dotnet --info` matches the frameworks you want to build (8.0/9.0).
- If NuGet restore fails, delete the `~/.nuget/packages` cache or run `dotnet restore` with verbosity to inspect failures.

## Contributing

Want to contribute? Check out our [Contributing Guide](CONTRIBUTING.md)!

We welcome:
- 🐛 Bug fixes
- ✨ New features
- 📝 Documentation improvements
- 🧪 Tests and examples
- 🔧 Tools and utilities

See the repository for contribution guidelines. Keep changes small and test builds on your target runtime.

## Documentation

- 📖 [Quick Reference Guide](QUICK_REFERENCE.md) - Code snippets for common tasks
- 🔀 [Migration Guide](MIGRATION.md) - Upgrading between releases, from 2.x to the latest
- 📁 [Examples](Programs/examples/README.md) - Sample applications
- 🔧 [Tools](Programs/tools/README.md) - Utility programs
- 🤝 [Contributing Guide](CONTRIBUTING.md) - How to contribute

---

For more project-specific details check the `Programs/examples/` folders or the individual project README files.

[![LibreMetaverse NuGet-Release](https://img.shields.io/nuget/v/libremetaverse.svg?label=LibreMetaverse)](https://www.nuget.org/packages/LibreMetaverse/) ![GitHub Release Date](https://img.shields.io/github/release-date/cinderblocks/libremetaverse)
[![BSD Licensed](https://img.shields.io/github/license/cinderblocks/libremetaverse)](https://github.com/cinderblocks/libremetaverse/blob/master/LICENSE.txt)
[![NuGet Downloads](https://img.shields.io/nuget/dt/LibreMetaverse?label=NuGet%20downloads)](https://www.nuget.org/packages/LibreMetaverse/)
[![Release](https://github.com/cinderblocks/libremetaverse/actions/workflows/release.yml/badge.svg)](https://github.com/cinderblocks/libremetaverse/actions/workflows/release.yml) [![Tests](https://github.com/cinderblocks/libremetaverse/actions/workflows/ci-test.yml/badge.svg)](https://github.com/cinderblocks/libremetaverse/actions/workflows/ci-test.yml)
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/1cb97cd799c64ba49e2721f2ddda56ab)](https://www.codacy.com/gh/cinderblocks/libremetaverse/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=cinderblocks/libremetaverse&amp;utm_campaign=Badge_Grade)
[![Commits per month](https://img.shields.io/github/commit-activity/m/cinderblocks/libremetaverse/master)](https://www.github.com/cinderblocks/libremetaverse/)
![Keybase PGP](https://img.shields.io/keybase/pgp/cinder)  
[![ZEC](https://img.shields.io/keybase/zec/cinder)](https://keybase.io/cinder) [![BTC](https://img.shields.io/keybase/btc/cinder)](https://keybase.io/cinder)

## Contributors

<a href="https://github.com/cinderblocks/libremetaverse/graphs/contributors">
  <img src="https://contrib.rocks/image?repo=cinderblocks/libremetaverse" />
</a>
