```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse

A .NET client library for Second Life and OpenSimulator (and compatible worlds). Log in as an avatar, look around, move, chat, build, trade, and manage inventory, groups and land, with an asynchronous API, events for everything the simulator tells you, and `Microsoft.Extensions` logging and dependency injection throughout. It is a fork of libOpenMetaverse, which came from libSecondLife, kept current with the grids it talks to.

```
  Archiving · Utilities · Voice.Vivox · Voice.WebRTC
      │  built on the client
 [LibreMetaverse]  the client: login, managers, inventory
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

## Getting started

```csharp
using LibreMetaverse;

var client = new GridClient();
client.Self.IM += (sender, e) => Console.WriteLine($"{e.IM.FromAgentName}: {e.IM.Message}");

var login = client.Network.DefaultLoginParams("First", "Last", "password", "MyApp", "1.0.0");
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

if (!await client.Network.LoginAsync(login, timeout.Token))
{
    Console.WriteLine($"Login failed: {client.Network.LoginMessage}");
    return;
}

Console.WriteLine($"In {client.Network.CurrentSim?.Name} at {client.Self.SimPosition}");
client.Network.Logout();
```

With dependency injection, `services.AddGridClient(settings => settings.Logging.LogResends = false)` registers the client as a singleton, and as `IGridClient` for testing.

## What the client gives you

`GridClient` is the front door. Everything hangs off it:

| Property | Does |
|---|---|
| `Network` | login, logout, the circuit to each simulator |
| `Self` | your own avatar: position, movement, teleports, chat, instant messages, animations, money, mutes |
| `Avatars` | other avatars: names, profiles, picks, classifieds, interests |
| `Objects` | prims in view: updates, selection, editing, rezzing, materials, media |
| `Inventory` | folders and items, uploads of textures, meshes and materials |
| `Assets` | downloading and uploading assets, with a cache |
| `Appearance` | what the avatar wears, baking, outfits |
| `Parcels`, `Grid`, `Terrain` | land, the world map and regions, the heightfield |
| `Groups`, `Friends`, `Directory` | groups and their rosters, the friends list, search |
| `Sound`, `Environment`, `Animesh`, `Marketplace`, `InterestList` | sound triggers, sky and water settings, animated meshes, merchant tools, which object updates the simulator sends |

Calls that go out and come back are `async` and take a `CancellationToken`. What the simulator pushes arrives as events on the manager it concerns. `client.Settings` holds the timing, connection, packet, texture-pipeline, asset-cache, security and logging options.

## The packages around it

The client is the top of a stack of smaller packages, and it brings the lower ones with it: `LibreMetaverse.Types`, `StructuredData`, `Imaging.Abstractions`, `Assets`, `Protocol` and `Transport`. Their types are still in the `LibreMetaverse` namespace and are forwarded from this assembly, so code written against 3.1 keeps working. You can also reference a lower package on its own when you do not need a login.

Separate packages, not pulled in automatically:

| Package | For |
|---|---|
| `LibreMetaverse.ModelFormats` | loading Collada, glTF and OBJ models for upload; exporting meshes |
| `LibreMetaverse.Archiving` | reading and writing OpenSim region archives (OAR) |
| `LibreMetaverse.Utilities` | persistent login, staying in a region, typed-out chat |
| `LibreMetaverse.Voice.WebRTC`, `LibreMetaverse.Voice.Vivox` | voice chat |
| `LibreMetaverse.RLV` | RestrainedLove command handling |
| `LibreMetaverse.Rendering.MeshFoundry`, `.Simple`, `LibreMetaverse.PrimMesher` | turning prims, sculpts and meshes into geometry |
| `LibreMetaverse.Imaging.Skia` | an image codec for PNG, JPEG and BMP |

## Examples

The repository has small, complete programs: [SimpleBot](https://github.com/cinderblocks/libremetaverse/tree/master/Programs/examples/SimpleBot) (answers instant messages), [PrimInspector](https://github.com/cinderblocks/libremetaverse/tree/master/Programs/examples/PrimInspector) (looks at the objects around it) and [InventoryExplorer](https://github.com/cinderblocks/libremetaverse/tree/master/Programs/examples/InventoryExplorer) (browses and exports inventory).

## Depends on

.NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10. Besides the LibreMetaverse packages it uses `Microsoft.Extensions.Logging` and `DependencyInjection.Abstractions`, MessagePack, CoreJ2K and a handful of `System.*` backports.

## Install

```
dotnet add package LibreMetaverse
```

Upgrading from 3.1? Types that moved to the lower packages are forwarded, so most code only needs a rebuild; the migration guide lists the few members that changed.

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
