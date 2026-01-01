# OSDInspector

A command-line utility for working with OSD (OpenMetaverse Structured Data) files.

OSD is LibreMetaverse's flexible serialization format supporting multiple encodings (JSON, XML, Binary, Notation). This tool helps you inspect, validate, and convert OSD data.

## Features

- ?? **Inspect** - View OSD structure and contents
- ?? **Convert** - Convert between JSON, XML, Binary, and Notation formats
- ? **Validate** - Check if files are valid OSD
- ?? **Generate** - Create sample Primitive OSD data
- ?? **Parse** - Convert OSD back to Primitive objects

## Commands

### Inspect
Display the structure and contents of an OSD file:
```bash
OSDInspector inspect data.json
OSDInspector i prim.xml
```

### Convert
Convert between OSD formats:
```bash
OSDInspector convert input.json xml output.xml
OSDInspector convert data.xml json data.json
OSDInspector convert config.json binary config.bin
OSDInspector convert data.xml notation data.txt
```

Supported formats:
- `json` - JSON format (human-readable)
- `xml` - LLSD XML format
- `binary` - LLSD Binary format (compact)
- `notation` - LLSD Notation format

### Validate
Check if a file is valid OSD:
```bash
OSDInspector validate data.json
OSDInspector v config.xml
```

### Primitive Serialization
Create a sample Primitive and output as OSD:
```bash
OSDInspector prim-to-osd > cube.json
```

Parse an OSD file as a Primitive:
```bash
OSDInspector osd-to-prim cube.json
```

## Example Workflows

### Debug Primitive Data
```bash
# Generate sample primitive
OSDInspector prim-to-osd > sample_prim.json

# Inspect it
OSDInspector inspect sample_prim.json

# Convert to XML for easy reading
OSDInspector convert sample_prim.json xml sample_prim.xml
```

### Work with Configuration Files
```bash
# Validate configuration
OSDInspector validate config.json

# Convert to binary for efficiency
OSDInspector convert config.json binary config.bin

# Inspect binary file
OSDInspector inspect config.bin
```

### Data Exchange
```bash
# Convert between formats for interop
OSDInspector convert export.xml json export.json
OSDInspector convert data.json notation data.txt
```

## What You'll Learn

This tool demonstrates:
- OSD serialization and deserialization
- Multiple OSD format encodings
- `Primitive.GetOSD()` and `Primitive.FromOSD()` methods
- Type detection and conversion
- Binary vs text encoding tradeoffs
- Tree structure traversal and display

## OSD Format Details

### JSON
- Human-readable
- Standard JSON syntax
- Easy to edit manually
- Widely supported

### XML (LLSD XML)
- Structured format used by Second Life protocol
- Type-safe serialization
- More verbose than JSON

### Binary (LLSD Binary)
- Compact binary encoding
- Smallest file size
- Fast to parse
- Not human-readable

### Notation (LLSD Notation)
- Compact text format
- Less common
- Good for debugging

## Use Cases

- Debug serialized objects
- Convert data between systems
- Validate protocol messages
- Test OSD parsing
- Learn OSD structure
- Primitive data interchange
- Configuration file management

## Integration

The OSD system is used throughout LibreMetaverse for:
- Network protocol messages
- Object serialization
- Configuration data
- Asset metadata
- Capability responses
