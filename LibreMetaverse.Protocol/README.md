```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Protocol

The Second Life wire protocol with no client attached. Every UDP message the grid sends or accepts, as a class you can build, serialize and parse; the capability messages that travel over HTTP; the login request and response; and the records and enums that those messages carry. If it is a shape on the wire, it is defined here.

```
  Archiving · Utilities · Voice.Vivox · Voice.WebRTC
      │  built on the client
  LibreMetaverse   the client: login, managers, inventory
      │
  Transport        UDP socket, reliable circuit, throttle
      │
 [Protocol]        packets and capability messages
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

- **UDP packets**: a class for every message in the viewer's message template (`ChatFromViewerPacket`, `AgentUpdatePacket`, `ObjectUpdatePacket`, `StartPingCheckPacket` and the rest), generated from the template at build time. Each one serializes with `ToBytes()`, and `Packet.BuildPacket` parses bytes back into the right class, handling zero-coding.
- **`PacketDecoder`**: turns a packet into text. With Trace-level logging enabled it writes the header and every block and field, with decoders for the fields that are better shown as names than as numbers; otherwise it writes a one-line summary (type and sequence number). Useful in proxies, loggers and test tools.
- **Capability messages** (`LibreMetaverse.Messages.Linden`): the event-queue and capability bodies as typed classes with `Serialize` and `Deserialize`.
- **Login** (`LoginRequest`, `LoginResponseData`, `AccountLevelBenefits`): both directions, so a client can read a login reply and a login *server* can write one in the form viewers expect.
- **Records and enums**: `Group`, `GroupMember`, `GroupRole`, `Parcel` and `ParcelInfo`, `FriendInfo`, `InstantMessage`, `MuteEntry`, `GridRegion`, `TransactionInfo`, the parcel flags, `ScriptPermission`, `TeleportLureFlags` and the many other values the messages use.

## Example

```csharp
using LibreMetaverse;
using LibreMetaverse.Packets;

var ping = new StartPingCheckPacket { PingID = { PingID = 7, OldestUnacked = 0 } };
byte[] wire = ping.ToBytes();

int end = wire.Length - 1;
Packet parsed = Packet.BuildPacket(wire, ref end, new byte[4096]);
Console.WriteLine(PacketDecoder.PacketToString(parsed));   // the whole packet at Trace level, a summary line otherwise
```

## Use it when

You are writing something that speaks the protocol but is not a client: a packet capture or proxy, a protocol test harness, a bot that wants only a few messages, or, in time, a simulator. The sockets and the reliability machinery that carry the packets are in `LibreMetaverse.Transport`; the client with its managers is `LibreMetaverse`.

## Depends on

`LibreMetaverse.Assets`, `LibreMetaverse.StructuredData` and `LibreMetaverse.Types`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Protocol
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
