```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.Utilities

A few helpers that sit on top of the client for the jobs bots keep rewriting: logging in and staying logged in, holding a position in a region, and typing chat at a human pace.

```
  Archiving · [Utilities] · Voice.Vivox · Voice.WebRTC
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

- **`ConnectionManager.PersistentLogin`**: logs in and keeps trying. It waits and retries when the grid is down, when the previous session is still being logged out, and when the request times out or the reply cannot be read; it gives up at once on a wrong password or a banned account, and after five unrecognized errors.
- **`ConnectionManager.StayInSim`**: checks on a timer that the avatar is in the region you named and teleports it back to a position if it is not. `Stop` ends the checking.
- **`Realism`**: `Chat` and `ChatAsync` send a message as if typed, with the typing animation and sound, a characters-per-second pace and random pauses; `Shoot` aims, enters mouselook, clicks and leaves it.
- **`WaterType`**: a small enum for the kind of water at a position.

## Use it when

You are writing a bot or an unattended agent and want the usual "keep this avatar alive and in place" behavior without writing it yourself. It is deliberately small; anything else you need is on `GridClient`.

## Depends on

`LibreMetaverse`, `LibreMetaverse.StructuredData` and `LibreMetaverse.Types`. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.Utilities
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
