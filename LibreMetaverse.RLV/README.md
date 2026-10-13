```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.RLV

RestrainedLove (RLV) command handling, with no dependency on the rest of LibreMetaverse. RLV is how scripted objects, worn by an avatar, ask a viewer to restrict what the wearer can do: no chatting, no teleporting, no undressing, a limited camera. This package parses those commands, keeps track of who has restricted what, and answers the queries a script may send back. It identifies itself as RLV 3.4.3 / RLVa 2.4.2.

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
  StructuredData · Imaging.Abstractions
      │
  Types            UUID, vectors, quaternions, colors

  on their own: PrimMesher · Imaging.Skia · [RLV] · LslTools
```

This package stands apart from the stack: it depends on no other LibreMetaverse package.

## What is in it

- **`RlvService`**: the entry point. Feed it chat or instant-message text with `ProcessMessageAsync` and it applies the commands in it. It reports what happens in the world back to scripts (`ReportItemWornAsync`, `ReportItemAttachedAsync`, inventory offers accepted or declined and so on).
- **`RlvRestrictionManager`**: the live set of restrictions, which object set each one, and queries over them (`FindRestrictions`, `GetRestrictionsByType`), plus the locked-folder bookkeeping.
- **`RlvCommandProcessor`** parses and runs the commands. **`RlvPermissionsService`** answers "may the avatar do this now?" from the restrictions in force (`CanFly`, `CanJump`, `CanSit`, `CanUnsit` and the like).
- **`RlvBlacklist`**: behaviors you choose not to honor (`BlacklistBehavior`, `IsBlacklisted`).
- **Callbacks**: you give the service `IRlvQueryCallbacks` (what your viewer can answer: whether an object exists, whether the avatar is sitting and on what, camera settings, the active group, the inventory map, environment and debug settings) and `IRlvActionCallbacks` (what it can be told to do: reply on a channel, send an instant message, teleport, sit and unsit, attach, detach and remove outfit items, set the group, rotate, adjust height or field of view, change environment and debug settings). `RlvCallbacksDefault` and `RlvActionCallbacksDefault` are inert implementations, a starting point and a base for tests.

## Example

```csharp
using LibreMetaverse.RLV;

var rlv = new RlvService(new RlvCallbacksDefault(), new RlvActionCallbacksDefault(), enabled: true);

// A worn object called "Gag" says "@sendchat=n" to its wearer.
await rlv.ProcessMessageAsync("@sendchat=n", Guid.NewGuid(), "Gag");

foreach (RlvRestriction r in rlv.Restrictions.FindRestrictions("sendchat"))
    Console.WriteLine(r);   // RlvRestriction: Behavior=SendChat SenderName="Gag" Args=[]
```

## Use it when

You are adding RLV to a viewer or a bot built on any library, not only LibreMetaverse: you supply the callbacks that touch your own client, the package does the protocol and the bookkeeping. The `GridClient` is not needed.

## Depends on

`System.Collections.Immutable`, and `Microsoft.Bcl.HashCode` on .NET Framework. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.RLV
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
