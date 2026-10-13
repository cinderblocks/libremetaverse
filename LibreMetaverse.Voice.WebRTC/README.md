```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Voice.WebRTC

Voice chat for Second Life's WebRTC voice service. It negotiates a session through the region's voice capabilities, sends and receives Opus audio over WebRTC, and handles nearby voice, group and conference voice and one-to-one calls, with per-person mute and gain. Audio capture and playback are done through SIPSorcery, with SDL3 or SoundFlow behind it.

```
  Archiving · Utilities · Voice.Vivox · [Voice.WebRTC]
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

- **`VoiceManager`**: construct it with your logged-in `GridClient`.
  - Nearby voice: `ConnectPrimaryRegionAsync`, `RequestParcelVoiceInfoAsync`, `Disconnect`. `IsVoiceAllowedAt` tells you whether the region offers voice.
  - Groups and conferences: `JoinGroupVoiceAsync`, `LeaveGroupVoiceAsync`, `JoinConferenceVoiceAsync`, and lists of the sessions and peers you are in.
  - Calls: `AcceptIncomingP2PCallAsync` and `DeclineIncomingP2PCall`.
  - People: `SetPeerMute`, `SetPeerGain`, `GetKnownPeers`, and `SendDataChannelMessage` for the WebRTC data channel.
  - Test audio: `PlayWavAsMic` and `PlayRawFile` feed a file into the microphone channel.
- **`AudioDevice`**, **`PeerManager`**, **`VoiceSession`**: the devices and the per-peer state.
- **`IVoiceLogger`**: where voice logging goes; `LibreMetaverseVoiceLogger` sends it to the client's logger.

## Example

```csharp
using LibreMetaverse.Voice.WebRTC;

var voice = new VoiceManager(client);          // client is a logged-in GridClient
if (VoiceManager.IsVoiceAllowedAt(client))
{
    bool joined = await voice.ConnectPrimaryRegionAsync();
    Console.WriteLine(joined ? "On voice" : "Could not join");
}
```

## Use it when

You want your bot or viewer to speak and listen in Second Life. This is the current voice service. `LibreMetaverse.Voice.Vivox` drives the older Vivox one. Native audio libraries come with the package's dependencies; check that the platform you ship on is covered.

## Depends on

`LibreMetaverse`, `LibreMetaverse.StructuredData`, SIPSorcery with its SDL3 and SoundFlow media packages, and `System.Text.Json`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Voice.WebRTC
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
