```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.StructuredData

LLSD, the Linden Lab Structured Data format that Second Life and OpenSimulator use for capabilities, login, inventory and most of what is not a UDP packet. This package parses and writes it in each of its encodings and gives you one tree to work with whichever form it came in.

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
  [StructuredData] · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · RLV · LslTools
```

A name in brackets is this package. Packages use the ones beneath them; the Depends on section below is exact.

## What is in it

- **The tree**: `OSD` and its kinds, `OSDMap`, `OSDArray`, `OSDString`, `OSDInteger`, `OSDReal`, `OSDBoolean`, `OSDUUID`, `OSDDate`, `OSDUri` and `OSDBinary`. The conversion methods (`AsString()`, `AsInteger()`, `AsUUID()`, `AsVector3()` and so on) are on every kind, and a kind that cannot be converted gives the type's default (zero, an empty string, `UUID.Zero`) instead of throwing.
- **The formats**: `OSDParser` reads and writes LLSD **XML**, **binary**, **notation**, **JSON** and the **protobuf** encoding, from strings, byte arrays and streams. `OSDFormat` names the three (XML, JSON, binary) that capabilities exchange.
- **Errors**: `OSDException` for input that is not valid in the format asked for.

## Example

```csharp
using LibreMetaverse.StructuredData;

OSD parsed = OSDParser.Deserialize("<llsd><map><key>name</key><string>Ahern</string></map></llsd>");
var map = (OSDMap)parsed;
map["visits"] = OSD.FromInteger(3);

string json = OSDParser.SerializeJsonString(map);
byte[] binary = OSDParser.SerializeLLSDBinary(map);
Console.WriteLine($"{map["name"].AsString()}: {json.Length} chars as JSON, {binary.Length} bytes as binary");
```

`OSDParser.Deserialize` tells XML, binary, protobuf and JSON apart from the start of the data, so the same call reads a capability reply whichever of those the server chose. Notation has its own call, `DeserializeLLSDNotation`.

## Use it when

You are talking to a grid's HTTP services without the client, writing a tool that inspects or converts LLSD (the repository's `OSDInspector` does this), or building the server side of a capability. It depends only on `LibreMetaverse.Types`.

## Depends on

.NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10. It uses `System.Text.Json` for the JSON form, and `Microsoft.Bcl.AsyncInterfaces` on the older targets.

## Install

```
dotnet add package LibreMetaverse.StructuredData
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
