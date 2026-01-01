# LibreMetaverse Tools

Command-line utilities for working with LibreMetaverse data and protocols.

## Available Tools

### OSDInspector
A utility for inspecting, converting, and validating OSD (OpenMetaverse Structured Data) files.

**Features:**
- Inspect OSD structure
- Convert between JSON, XML, Binary, and Notation formats
- Validate OSD files
- Serialize/deserialize Primitives to/from OSD

[?? Full Documentation](OSDInspector/README.md)

**Quick Start:**
```bash
# Inspect a file
dotnet run --project OSDInspector -- inspect data.json

# Convert formats
dotnet run --project OSDInspector -- convert data.json xml data.xml

# Validate
dotnet run --project OSDInspector -- validate config.json
```

---

## Building Tools

Build all tools from the solution root:
```bash
dotnet build Programs/tools/
```

Or build individually:
```bash
dotnet build Programs/tools/OSDInspector/OSDInspector.csproj
```

## Running Tools

After building, run from the output directory:
```bash
cd Programs/tools/OSDInspector/bin/Release/net8.0/
dotnet OSDInspector.dll [arguments]
```

Or use `dotnet run` during development:
```bash
cd Programs/tools/OSDInspector
dotnet run -- [arguments]
```

## Tool Ideas

Looking to contribute? Here are some tool ideas:

- **PacketAnalyzer** - Analyze captured network packets
- **MeshConverter** - Convert between mesh formats
- **TextureProcessor** - Batch process textures
- **ScriptLinter** - Validate LSL scripts
- **AssetBundler** - Package assets for distribution
- **LogParser** - Parse and analyze LibreMetaverse logs
- **BenchmarkRunner** - Performance benchmarking suite
- **ProtocolTester** - Test protocol compliance

## Differences from Examples

**Examples** (`Programs/examples/`):
- Interactive applications
- Demonstrate library features
- Educational focus
- Connect to live grids

**Tools** (`Programs/tools/`):
- Command-line utilities
- Single-purpose focused
- Data processing
- Often work offline

Both are valuable for learning and using LibreMetaverse!
