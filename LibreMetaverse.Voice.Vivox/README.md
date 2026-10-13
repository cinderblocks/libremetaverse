```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Voice.Vivox

Voice chat through Vivox, the service Second Life used before WebRTC. It drives Vivox's `SLVoice` daemon over its local control connection: it starts the daemon, creates a connector, logs the avatar in to the voice account, and manages sessions and participants, so your client can join spatial and group channels and see who is talking.

```
  Archiving · Utilities · [Voice.Vivox] · Voice.WebRTC
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

- **`VoiceGateway`** and **`VoiceManager`**: start and supervise the daemon, send it requests and turn its XML events into callbacks and .NET events (`SessionStateChange`, `ParticipantStateChange` and so on).
- **Sessions and participants**: `VoiceSession`, `VoiceParticipant`, with state, mute and position.
- **Devices and audio**: capture and render device lists, microphone and speaker levels, `VoicePosition` for 3D placement.
- **`TCPPipe`**: the control connection to the daemon.

## Use it when

You need to keep a client working with a grid that still offers Vivox voice. For Second Life itself, prefer `LibreMetaverse.Voice.WebRTC`. This package needs the Vivox `SLVoice` executable, which it is not able to ship: it looks for one in a `voice` folder beside your application or in the Second Life viewer's install folder. That makes it mainly a Windows package.

## Depends on

`LibreMetaverse`, `LibreMetaverse.StructuredData` and `LibreMetaverse.Types`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Voice.Vivox
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
