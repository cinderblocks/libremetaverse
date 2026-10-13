```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Transport

The UDP half of the Second Life circuit, with no client attached: the socket loop, the datagram buffer, the traffic throttle, and a reliable circuit that sequences, acknowledges, resends and de-duplicates packets the way the viewer's own circuit does. It is what a client uses to talk to a simulator, and what a simulator would use to talk to a client.

```
  Archiving · Utilities · Voice.Vivox · Voice.WebRTC
      │  built on the client
  LibreMetaverse   the client: login, managers, inventory
      │
 [Transport]       UDP socket, reliable circuit, throttle
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

- **`ReliableCircuit`**: sends packets with sequence numbers, remembers the ones that asked for an acknowledgement, resends them until they are acknowledged or give up, appends acknowledgements to outgoing packets (up to 250 at a time, as the viewer does), and drops a packet it has already seen. It does no I/O of its own: you give it a function that transmits a datagram, and call `SendAcks()` and `ResendUnacked()` on your own timer.
- **`UDPBase`**: an abstract socket loop. Subclass it, say what to do with a received or sent packet, and call `Start()`. Datagrams are received asynchronously and handed to you through a bounded queue, so a slow handler holds the socket back rather than losing packets.
- **`UDPPacketBuffer`** and **`OutgoingDatagram`**: a datagram and its bookkeeping (sequence number, resend count, packet type).
- **`ThrottleRates`**: the eight throttle categories (resend, land, wind, cloud, task, texture, asset, total) and their wire form, the bytes of an `AgentThrottle` packet.
- **`IncomingPacketIDCollection`**: the record of recently received sequence numbers that duplicate detection uses.

## Example

```csharp
using System.Net;
using LibreMetaverse;
using LibreMetaverse.Packets;

var remote = new IPEndPoint(IPAddress.Loopback, 9000);
var circuit = new ReliableCircuit(remote, buffer =>
{
    // write buffer.Data[0..buffer.DataLength] to your socket
});

circuit.SendPacket(new StartPingCheckPacket { PingID = { PingID = 1 } });

// on your timer, a few times a second:
circuit.SendAcks();        // acknowledge what has been received
circuit.ResendUnacked();   // resend what the other end has not acknowledged

Console.WriteLine($"{circuit.UnackedCount} packets waiting for an acknowledgement");
```

## How it is checked

The circuit's behavior was compared with the viewer's own circuit code (the 250-acknowledgement limit, resending the original bytes without acknowledgements, the 255-block limit, and what a ping carries), and its test suite runs it against a real socket. The few places where it knowingly differs are written down in the tests.

## Use it when

You are building anything that has a UDP conversation with a grid and wants the reliability layer without the client: a simulator, a protocol test rig, a load generator. The packets themselves come from `LibreMetaverse.Protocol`.

## Depends on

`LibreMetaverse.Protocol`, `LibreMetaverse.Assets` and `LibreMetaverse.Types`, plus `System.Threading.Channels`, `System.Threading.RateLimiting` and `Microsoft.Extensions.ObjectPool`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Transport
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
