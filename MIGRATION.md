# Migration Guide: 3.0.0 → 3.1.0

LibreMetaverse 3.1.0 removes SkiaSharp as a dependency of every core assembly (`LibreMetaverse`, `LibreMetaverse.PrimMesher`, `LibreMetaverse.Rendering.MeshFoundry`, `LibreMetaverse.Rendering.Simple`). Image decoding is now behind a new `ITextureCodec` abstraction, with SkiaSharp available as an opt-in backend. This is a breaking change everywhere `SKBitmap` previously appeared in a public signature.

---

## 1. New packages

- **`LibreMetaverse.Imaging.Abstractions`** — `ManagedImage` (moved here from `LibreMetaverse`, same namespace `LibreMetaverse.Imaging`) and the new `ITextureCodec` interface. No package dependencies of its own. Pulled in transitively by the `LibreMetaverse` package, so most consumers don't need to reference it directly.
- **`LibreMetaverse.Imaging.Skia`** — `SkiaTextureCodec : ITextureCodec`, a SkiaSharp-backed implementation. Reference this package (and pass a `new SkiaTextureCodec()` where an `ITextureCodec` is expected) if your application needs to decode arbitrary image formats (PNG, JPEG, BMP, etc.) — for example, loading Collada model textures or handling image uploads from disk. Not needed if you only ever work with `.tga`/JPEG2000 image data, which have dedicated Skia-free paths (see below).

```xml
<!-- Add this if you decode arbitrary image files anywhere in your application -->
<PackageReference Include="LibreMetaverse.Imaging.Skia" Version="3.1.*" />
```

---

## 2. `IRendering`: `SKBitmap` → `ManagedImage`

`GenerateSimpleSculptMesh`/`GenerateFacetedSculptMesh` on `IRendering` (and its implementations, `SimpleRenderer`/`MeshFoundry`) now take a `ManagedImage` sculpt texture instead of an `SKBitmap`.

```csharp
// Before
FacetedMesh? mesh = renderer.GenerateFacetedSculptMesh(prim, sculptBitmap, DetailLevel.Medium);

// After
FacetedMesh? mesh = renderer.GenerateFacetedSculptMesh(prim, sculptImage, DetailLevel.Medium);
// where sculptImage is a LibreMetaverse.Imaging.ManagedImage — e.g. AssetTexture.Image
// after decoding the sculpt texture asset, or SkiaTextureCodec.Decode(stream) for
// arbitrary image sources.
```

If you implement `IRendering` yourself, update your method signatures accordingly.

---

## 3. `PrimMesher.SculptMesh` / `SculptMap`: `SKBitmap` → `ManagedImage`

All `SKBitmap`-typed constructors and parameters have been replaced with `ManagedImage`:

| 3.0.0 | 3.1.0 |
|-------|-------|
| `new SculptMap(SKBitmap bm, int lod)` | `new SculptMap(ManagedImage image, int lod)` |
| `new SculptMesh(SKBitmap sculptBitmap, SculptType sculptType, int lod, bool viewerMode)` | `new SculptMesh(ManagedImage sculptImage, SculptType sculptType, int lod, bool viewerMode)` |
| `new SculptMesh(SKBitmap sculptBitmap, SculptType sculptType, int lod, bool viewerMode, bool mirror, bool invert)` | `new SculptMesh(ManagedImage sculptImage, SculptType sculptType, int lod, bool viewerMode, bool mirror, bool invert)` |

The file-decoding convenience constructors, which previously called into SkiaSharp directly, now take an `ITextureCodec` so `PrimMesher` itself has no image-library dependency:

```csharp
// Before
var mesh = new SculptMesh(fileName, sculptType, lod, viewerMode, mirror, invert);
var mesh2 = sculptMesh.SculptMeshFromFile(fileName, sculptType, lod, viewerMode);

// After — pass a codec (e.g. from LibreMetaverse.Imaging.Skia)
var codec = new SkiaTextureCodec();
var mesh = new SculptMesh(fileName, codec, sculptType, lod, viewerMode, mirror, invert);
var mesh2 = sculptMesh.SculptMeshFromFile(fileName, codec, sculptType, lod, viewerMode);
```

`LibreMetaverse.PrimMesher` no longer references SkiaSharp at all.

---

## 4. `Targa`: `SKBitmap`-based `Decode`/`Encode` removed

`Targa.Decode(string)`, `Targa.Decode(Stream)`, and `Targa.Encode(SKBitmap)` have been deleted. Use the `ManagedImage`-native equivalents, which have no SkiaSharp dependency:

```csharp
// Before
SKBitmap bitmap = Targa.Decode(fileName);
byte[] tga = Targa.Encode(bitmap);

// After
ManagedImage image = Targa.DecodeToManagedImage(fileName);
byte[] tga = Targa.Encode(image);
```

---

## 5. `ColladaLoader` constructor takes an optional `ITextureCodec`

```csharp
// Before
var loader = new ColladaLoader();

// After — unchanged if your model only references .tga/.jp2/.j2c textures.
// Pass a codec if any referenced texture is a PNG/JPEG/other arbitrary format:
var loader = new ColladaLoader(new SkiaTextureCodec());
```

If a model references a non-TGA, non-JPEG2000 texture and no codec was supplied, `ColladaLoader` throws an `InvalidOperationException` with a message pointing at `LibreMetaverse.Imaging.Skia`, rather than failing with a confusing SkiaSharp error.

---

## 6. `CoreJ2K.Skia` no longer a dependency

`LibreMetaverse` previously pulled in `CoreJ2K.Skia` (and, transitively, an inconsistently-versioned copy of SkiaSharp). JPEG2000 encode/decode now goes through a `ManagedImage`-native CoreJ2K backend exclusively. If you referenced `CoreJ2K.Skia` yourself to interoperate with `LibreMetaverse`'s texture pipeline, you no longer need to.

---

## Quick checklist (3.0.0 → 3.1.0)

- [ ] Add a `PackageReference` to `LibreMetaverse.Imaging.Skia` if your app decodes arbitrary (non-TGA, non-J2K) image formats anywhere
- [ ] Update any custom `IRendering` implementation's sculpt-mesh methods from `SKBitmap` to `ManagedImage`
- [ ] Update `PrimMesher.SculptMesh`/`SculptMap` construction from `SKBitmap` to `ManagedImage`; pass an `ITextureCodec` to the file-decoding convenience constructors
- [ ] Replace `Targa.Decode(...)`/`Targa.Encode(SKBitmap)` with `Targa.DecodeToManagedImage(...)`/`Targa.Encode(ManagedImage)`
- [ ] Pass an `ITextureCodec` to `new ColladaLoader(...)` if any Collada model you load references a non-TGA/non-J2K texture
- [ ] Remove any direct `CoreJ2K.Skia` package reference that existed solely to interoperate with LibreMetaverse's texture pipeline

---

# Migration Guide: 2.6.x → 3.0

LibreMetaverse 3.0 is a major version with intentional breaking changes across the entire public API. This guide covers every change a consuming application will need to address. Changes are grouped by category, with before/after examples for the most common cases.

---

## 1. Namespace

The root namespace changed from `OpenMetaverse` to `LibreMetaverse`. Update every `using` directive and fully-qualified reference in your project.

```csharp
// Before
using OpenMetaverse;
using OpenMetaverse.StructuredData;

// After
using LibreMetaverse;
using LibreMetaverse.StructuredData;
```

NuGet package IDs are unchanged (`LibreMetaverse`, `LibreMetaverse.StructuredData`, etc.).

---

## 2. Settings restructure

The flat `Settings` class with `SCREAMING_SNAKE_CASE` fields has been replaced by a set of named, grouped sub-objects. All instance settings are now properties on `GridClient.Settings`; process-wide settings are `static` fields on `Settings`.

### Timeout / timing settings

| 2.6 | 3.0 |
|-----|-----|
| `client.Settings.TELEPORT_TIMEOUT` | `client.Settings.Timing.TeleportTimeout` |
| `client.Settings.LOGOUT_TIMEOUT` | `client.Settings.Timing.LogoutTimeout` |
| `client.Settings.CAPS_TIMEOUT` | `client.Settings.Timing.CapsTimeout` |
| `client.Settings.LOGIN_TIMEOUT` | `client.Settings.Timing.LoginTimeout` |
| `client.Settings.RESEND_TIMEOUT` | `client.Settings.Timing.ResendTimeout` |
| `client.Settings.SIMULATOR_TIMEOUT` | `client.Settings.Timing.SimulatorTimeout` |
| `client.Settings.MAP_REQUEST_TIMEOUT` | `client.Settings.Timing.MapRequestTimeout` |
| `client.Settings.TRANSFER_TIMEOUT` | `client.Settings.Timing.TransferTimeout` |
| `client.Settings.DEFAULT_AGENT_UPDATE_INTERVAL` | `client.Settings.Timing.AgentUpdateInterval` |
| `client.Settings.INTERPOLATION_INTERVAL` | `client.Settings.Timing.InterpolationInterval` |

### Connection settings

| 2.6 | 3.0 |
|-----|-----|
| `client.Settings.LOGIN_SERVER` | `client.Settings.Connection.LoginServer` |
| `client.Settings.MFA_ENABLED` | `client.Settings.Connection.MfaEnabled` |
| `Settings.AGNI_LOGIN_SERVER` | `Settings.AgniLoginServer` |
| `Settings.ADITI_LOGIN_SERVER` | `Settings.AditiLoginServer` |

### Agent / packet / world settings

| 2.6 | 3.0 |
|-----|-----|
| `client.Settings.SEND_AGENT_UPDATES` | `client.Settings.Agent.SendUpdates` |
| `client.Settings.SEND_AGENT_UPDATES_REGULARLY` | `client.Settings.Agent.SendUpdatesRegularly` |
| `client.Settings.SEND_AGENT_APPEARANCE` | `client.Settings.Agent.SendAppearance` |
| `client.Settings.SEND_AGENT_THROTTLE` | `client.Settings.Agent.SendThrottle` |
| `client.Settings.DISABLE_AGENT_UPDATE_DUPLICATE_CHECK` | `client.Settings.Agent.DisableUpdateDuplicateCheck` |
| `client.Settings.MAX_PENDING_ACKS` | `client.Settings.Packets.MaxPendingAcks` |
| `client.Settings.MAX_RESEND_COUNT` | `client.Settings.Packets.MaxResendCount` |
| `client.Settings.ENABLE_SIMSTATS` | `client.Settings.Packets.EnableSimStats` |

### Texture pipeline settings

| 2.6 | 3.0 |
|-----|-----|
| `client.Settings.MAX_CONCURRENT_TEXTURE_DOWNLOADS` | `client.Settings.TexturePipeline.MaxConcurrentDownloads` |
| `client.Settings.PIPELINE_REQUEST_TIMEOUT` | `client.Settings.TexturePipeline.RequestTimeout` |
| `Settings.PIPELINE_REFRESH_INTERVAL` | `Settings.TexturePipelineRefreshInterval` |

### Asset cache settings

| 2.6 | 3.0 |
|-----|-----|
| `client.Settings.ASSET_CACHE_MAX_SIZE` | `client.Settings.AssetCache.MaxSize` |

### Process-wide statics

| 2.6 | 3.0 |
|-----|-----|
| `Settings.USER_AGENT` | `Settings.UserAgent` |
| `Settings.RESOURCE_DIR` | `Settings.ResourceDir` |
| `Settings.BIND_ADDR` | `Settings.BindAddress` |
| `Settings.MAX_HTTP_CONNECTIONS` | `Settings.MaxHttpConnections` |
| `Settings.SIMULATOR_POOL_TIMEOUT` | `Settings.SimulatorPoolTimeout` |

### Constants

| 2.6 | 3.0 |
|-----|-----|
| `Settings.PING_INTERVAL` | `Settings.PingInterval` |
| `Settings.NETWORK_TICK_INTERVAL` | `Settings.NetworkTickInterval` |
| `Settings.MAX_PACKET_SIZE` | `Settings.MaxPacketSize` |
| `Settings.MAX_SEQUENCE` | `Settings.MaxSequence` |
| `Settings.ENABLE_INVENTORY_STORE` | `Settings.EnableInventoryStore` |
| `Settings.ENABLE_LIBRARY_STORE` | `Settings.EnableLibraryStore` |

---

## 3. Async API migration

The entire network-calling surface of the library is now `async Task<T>`. Callback-delegate parameters have been removed everywhere in favour of awaitable return values. Every blocking sync wrapper has been deleted.

The general migration pattern is:

```csharp
// Before — pass a callback, block or wait externally
bool result = client.Self.Teleport("Hippo Hollow", new Vector3(128, 128, 25));

// After — await the Task
bool result = await client.Self.TeleportAsync("Hippo Hollow", new Vector3(128, 128, 25));
```

### AgentManager (`client.Self`)

| 2.6 | 3.0 |
|-----|-----|
| `Teleport(UUID)` | `await TeleportAsync(UUID, CancellationToken)` |
| `Teleport(ulong, Vector3)` | `await TeleportAsync(ulong, Vector3, CancellationToken)` |
| `Teleport(ulong, Vector3, Vector3)` | `await TeleportAsync(ulong, Vector3, Vector3, CancellationToken)` |
| `Teleport(string, Vector3)` | `await TeleportAsync(string, Vector3, CancellationToken)` |
| `Teleport(string, Vector3, Vector3)` | `await TeleportAsync(string, Vector3, Vector3, CancellationToken)` |
| `GoHome()` | `await GoHomeAsync(CancellationToken)` |
| `PlayGesture(UUID)` | `await PlayGestureAsync(UUID, CancellationToken)` |
| `GetAttachmentResources(callback)` | `var (ok, msg) = await GetAttachmentResourcesAsync()` |
| `SetAgentAccessAsync(level, callback)` | `var result = await SetAgentAccessAsync(level)` — callback parameter removed |

### AvatarManager (`client.Avatars`)

| 2.6 | 3.0 |
|-----|-----|
| `GetDisplayNames(ids, callback)` | `var (ok, names, missing) = await GetDisplayNamesAsync(ids)` |
| `RequestAgentProfile(id, callback)` | `var (ok, profile) = await RequestAgentProfileAsync(id)` |

### AppearanceManager (`client.Appearance`)

| 2.6 | 3.0 |
|-----|-----|
| `ReplaceOutfit(wearables)` | `await ReplaceOutfitAsync(wearables)` |
| `WearOutfit(wearables)` | `await WearOutfitAsync(wearables)` |

### GridManager (`client.Grid`)

| 2.6 | 3.0 |
|-----|-----|
| `GetGridRegion(string, layer, out GridRegion)` → `bool` | `GridRegion? r = await GetGridRegionAsync(string, layer, CancellationToken)` |
| `GetGridRegion(ulong, layer, out GridRegion)` → `bool` | `GridRegion? r = await GetGridRegionAsync(ulong, layer, CancellationToken)` |
| `MapItems(handle, type, layer, timeout)` → `List<MapItem>` | `List<MapItem> items = await MapItemsAsync(handle, type, layer, CancellationToken)` |
| `RequestMapLayer(layer)` (fire-and-forget void) | `await RequestMapLayerAsync(layer, CancellationToken)` |

`GetGridRegionAsync` returns `GridRegion?` (nullable struct) — check for `null` instead of the old `bool` return value:

```csharp
// Before
if (client.Grid.GetGridRegion("Hippo Hollow", GridLayerType.Objects, out var region))
    Console.WriteLine(region.Name);

// After
var region = await client.Grid.GetGridRegionAsync("Hippo Hollow", GridLayerType.Objects);
if (region != null)
    Console.WriteLine(region.Value.Name);
```

### NetworkManager (`client.Network`)

| 2.6 | 3.0 |
|-----|-----|
| `Logout()` | `Logout()` still available; `await LogoutAsync(CancellationToken)` added |

### ObjectManager (`client.Objects`)

| 2.6 | 3.0 |
|-----|-----|
| `NavigateObjectMedia(...)` (void) | `await NavigateObjectMediaAsync(...)` |
| `UpdateObjectMedia(...)` (void) | `await UpdateObjectMediaAsync(...)` |
| `RequestObjectMedia(prim, callback)` | `var (ok, ver, entries) = await RequestObjectMediaAsync(prim)` — callback removed |

### ParcelManager (`client.Parcels`)

| 2.6 | 3.0 |
|-----|-----|
| `GetParcelResources(id, callback)` | `var (ok, info) = await GetParcelResourcesAsync(id)` — callback removed |
| `RequestRemoteParcelID(...)` (sync wrapper) | deleted — no replacement needed; use the async cap directly |

### InventoryManager (`client.Inventory`)

All upload and script methods had their callback parameters removed and now return tuples via `Task<T>`:

| 2.6 | 3.0 return type |
|-----|-----|
| `RequestUploadNotecardAsset(..., callback)` | `Task<(bool, string, UUID, UUID)>` |
| `RequestUpdateNotecardTask(..., callback)` | `Task<(bool, string, UUID, UUID)>` |
| `RequestUploadGestureAsset(..., callback)` | `Task<(bool, string, UUID, UUID)>` |
| `RequestUpdateScriptAgentInventory(..., callback)` | `Task<(bool, bool, string, List<string>?, UUID, UUID)>` |
| `RequestUpdateScriptTask(..., callback)` | `Task<(bool, bool, string, List<string>?, UUID, UUID)>` |

The sync helpers `FindObjectByPath` and `GetInventoryRecursive` have been deleted. Use the existing async variants (`FindObjectsByPath`, `FetchInventoryDescendants`).

### GroupManager (`client.Groups`)

| 2.6 | 3.0 |
|-----|-----|
| `RequestBannedAgents(id, callback, ct)` | subscribe to `BannedAgents` event, then `await RequestBannedAgents(id, ct)` — callback removed |
| `RequestBanAction(id, action, agents, callback, ct)` | `await RequestBanAction(id, action, agents, ct)` — callback removed |

### EstateTools (`client.Estate`)

`SetRegionInfo()` sync wrapper deleted (was a blocking wrapper with no callers outside the library). Use the async variant.

### Capability rate limiting — `CapRateLimitException`

A new `CapRateLimitException` (inherits `Exception`) is thrown when a simulator cap responds with "cap invocation rate exceeded". If you have broad `catch (Exception)` handlers around cap calls you may want to handle it explicitly:

```csharp
try
{
    await client.Objects.RequestMaterialsAsync(sim, ct);
}
catch (CapRateLimitException ex)
{
    Logger.Log($"Rate limited: {ex.Message} — backing off", Helpers.LogLevel.Warning);
}
```

---

## 4. Removed caches and application-layer state

Several library-held caches were removed. The library no longer maintains unbounded in-memory state on behalf of the application.

### `GroupManager.GroupName2KeyCache` removed

The `LockingDictionary<UUID, string> GroupName2KeyCache` field no longer exists. Applications that relied on it for group-name lookups should maintain their own dictionary, populated by subscribing to `GroupManager.GroupNamesReply`.

```csharp
// Before
if (client.Groups.GroupName2KeyCache.TryGetValue(groupID, out var name)) { ... }

// After — subscribe and maintain your own cache
var groupNames = new Dictionary<UUID, string>();
client.Groups.GroupNamesReply += (s, e) =>
{
    foreach (var kvp in e.GroupNames)
        groupNames[kvp.Key] = kvp.Value;
};
client.Groups.RequestGroupName(groupID);
```

### `Simulator.AvatarPositions` removed

The per-simulator coarse position map (`Simulator.AvatarPositions`) has been removed. Coarse positions are now delivered exclusively through the `GridManager.CoarseLocationUpdate` event, which carries a full snapshot on every update.

```csharp
// Before
if (sim.AvatarPositions.TryGetValue(agentID, out var pos)) { ... }

// After — subscribe to CoarseLocationUpdate
client.Grid.CoarseLocationUpdate += (s, e) =>
{
    if (e.Positions.TryGetValue(agentID, out var pos)) { ... }
};
```

`CoarseLocationUpdateEventArgs` now exposes:
- `Positions` — `IReadOnlyDictionary<UUID, Vector3>`: full current snapshot for the simulator
- `NewEntries` — agents who appeared since the last update
- `RemovedEntries` — agents who left since the last update

### `GestureManager` removed

The `GestureManager` class (chat-based auto-trigger detector with its `_gestures` and `_triggersByWord` caches) has been deleted. It was opt-in application-layer logic that did not belong in the library.

Applications that used it should subscribe to outgoing chat events and call `AgentManager.PlayGestureAsync(UUID)` directly when a trigger word is detected.

---

## 5. `InventoryItem` / `InventoryFolder` `GetHashCode` behavior change

`GetHashCode()` on inventory nodes now returns a hash based on `UUID` only, matching the behavior of `Equals()`. Previously it hashed all content fields, which made dictionary lookups break silently whenever any metadata field was mutated after insertion.

If your code uses `InventoryItem` or `InventoryFolder` as dictionary keys and depends on content-equality hashing, switch to a custom `IEqualityComparer<T>`.

---

## 6. Vector3 / Quaternion / Matrix4 struct mutation removed

Mutating instance methods on the math types have been replaced with static value-returning methods. Code that called `.Normalize()`, `.Cross()`, etc. on a local variable and expected the variable to be modified in-place will silently stop working — the method no longer modifies the receiver.

```csharp
// Before
Vector3 v = someVector;
v.Normalize();

// After
Vector3 v = Vector3.Normalize(someVector);
```

Common cases:

| 2.6 | 3.0 |
|-----|-----|
| `v.Normalize()` | `v = Vector3.Normalize(v)` |
| `q.Normalize()` | `q = Quaternion.Normalize(q)` |
| `v.Cross(other)` | `v = Vector3.Cross(v, other)` |

---

## 7. Rendering: Meshmerizer removed

`LibreMetaverse.Rendering.Meshmerizer` is no longer supported. Use `LibreMetaverse.Rendering.MeshFoundry` instead.

Update your NuGet reference:
```xml
<!-- Before -->
<PackageReference Include="LibreMetaverse.Rendering.Meshmerizer" Version="2.*" />

<!-- After -->
<PackageReference Include="LibreMetaverse.Rendering.MeshFoundry" Version="3.*" />
```

---

## 8. Deleted sync utility methods

The following were sync-over-async wrappers or dead code with no callers; they have been deleted with no replacement:

| Deleted | Notes |
|---------|-------|
| `AsyncHelper.Sync(...)` | Use `await` instead |
| `AssetCache.Prune()` | Use `await PruneAsync()` |
| `AssetManager.RequestUpload(...)` (3 sync overloads) | Use the async upload methods |
| `InventoryManager.FindObjectByPath(...)` | Use `FindObjectsByPath(...)` |
| `InventoryManager.GetInventoryRecursive(...)` | Use `FetchInventoryDescendants(...)` |
| `EstateTools.SetRegionInfo()` | Use the async variant |
| `ParcelManager.RequestRemoteParcelID(...)` | Use the async cap directly |
| `Utils.GetRunningRuntime()` | Use `System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription` directly |

---

## 9. Async method renames

All `public async Task` methods that were missing the `Async` suffix have been renamed. Update every call site.

### AgentManager

| Before | After |
|--------|-------|
| `RetrieveInstantMessages(ct)` | `RetrieveInstantMessagesAsync(ct)` |
| `ChatterBoxAcceptInvite(id, ct)` | `ChatterBoxAcceptInviteAsync(id, ct)` |
| `StartIMConference(participants, id, ct)` | `StartIMConferenceAsync(participants, id, ct)` — now returns `Task` |
| `UpdateProfileHttp(profile, ct)` | `UpdateProfileAsync(profile, ct)` |
| `UpdateProfileNotesHttp(target, notes, ct)` | `UpdateProfileNotesAsync(target, notes, ct)` |

### EstateTools

| Before | After |
|--------|-------|
| `SendEstateChangeInfo(name, sun, flags)` | `SendEstateChangeInfoAsync(name, sun, flags, ct)` |
| `SetRegionInfoHttp(...)` *(public)* | made private — use `SetRegionInfoAsync(...)` |
| `SendEstateChangeInfoHttp(uri, ...)` *(public)* | made private — use `SendEstateChangeInfoAsync(...)` |

### GroupManager

| Before | After |
|--------|-------|
| `RequestBannedAgents(id, ct)` | `RequestBannedAgentsAsync(id, ct)` |
| `RequestBanAction(id, action, agents, ct)` | `RequestBanActionAsync(id, action, agents, ct)` |

### InventoryManager

| Before | After |
|--------|-------|
| `RequestFolderContents(folderID, ownerID, ...)` | `RequestFolderContentsAsync(folderID, ownerID, ...)` |
| `RequestFolderContents(batch, uri, ...)` | `RequestFolderContentsAsync(batch, uri, ...)` |
| `RequestFetchInventoryHttpAsync(items, ct, callback)` *(public)* | made private — use `RequestFetchInventoryAsync(items, ct, callback)` |

### ModelUploader

| Before | After |
|--------|-------|
| `Upload(ct)` | `UploadAsync(ct)` |
| `PrepareUpload(ct)` | `PrepareUploadAsync(ct)` |
| `PerformUpload(uri, ct)` | `PerformUploadAsync(uri, ct)` |

### ObjectManager

| Before | After |
|--------|-------|
| `RequestMaterials(sim, ct)` | `RequestMaterialsAsync(sim, ct)` |
| `RequestMaterials(sim, materials, ct)` | `RequestMaterialsAsync(sim, materials, ct)` |

### InventoryAISClient

| Before | After |
|--------|-------|
| `EmptyTrash(ct)` | `EmptyTrashAsync(ct)` |

### AppearanceManager

| Before | After |
|--------|-------|
| `GetCurrentOutfitFolder(ct)` | `GetCurrentOutfitFolderAsync(ct)` |

### CurrentOutfitFolder

| Before | After |
|--------|-------|
| `GetCurrentOutfitLinks(ct)` | `GetCurrentOutfitLinksAsync(ct)` |
| `GetWornAt(type, ct)` | `GetWornAtAsync(type, ct)` |
| `CanAttachItem(item, ct)` | `CanAttachItemAsync(item, ct)` |
| `CanDetachItem(item, ct)` | `CanDetachItemAsync(item, ct)` |
| `Attach(item, point, replace, ct)` | `AttachAsync(item, point, replace, ct)` |
| `Detach(item, ct)` | `DetachAsync(item, ct)` |
| `ReplaceOutfit(folderID, ct)` | `ReplaceOutfitAsync(folderID, ct)` |
| `AddToOutfit(item, replace, ct)` | `AddToOutfitAsync(item, replace, ct)` |
| `AddToOutfit(items, replace, ct)` | `AddToOutfitAsync(items, replace, ct)` |
| `RemoveFromOutfit(item, ct)` | `RemoveFromOutfitAsync(item, ct)` |
| `RemoveFromOutfit(items, ct)` | `RemoveFromOutfitAsync(items, ct)` |
| `FetchParent(item, ct)` | `FetchParentAsync(item, ct)` |
| `IsObjectDescendentOf(item, parentID, ct)` | `IsObjectDescendentOfAsync(item, parentID, ct)` |

### ICurrentOutfitPolicy / CompositeCurrentOutfitPolicy

| Before | After |
|--------|-------|
| `ReportItemChange(added, removed, ct)` | `ReportItemChangeAsync(added, removed, ct)` |

### InventoryManager (additional)

| Before | After |
|--------|-------|
| `RequestFindObjectByPath(folder, owner, path, ct)` | `RequestFindObjectByPathAsync(folder, owner, path, ct)` |

### LibreMetaverse.RLV — RlvService

| Before | After |
|--------|-------|
| `ProcessMessage(msg, id, name, ct)` | `ProcessMessageAsync(msg, id, name, ct)` |
| `ProcessInstantMessage(msg, id, ct)` | `ProcessInstantMessageAsync(msg, id, ct)` |
| `ReportSendPublicMessage(msg, ct)` | `ReportSendPublicMessageAsync(msg, ct)` |
| `ReportInventoryOfferAccepted(path, ct)` | `ReportInventoryOfferAcceptedAsync(path, ct)` |
| `ReportInventoryOfferDeclined(path, ct)` | `ReportInventoryOfferDeclinedAsync(path, ct)` |
| `ReportItemWorn(folderId, shared, type, ct)` | `ReportItemWornAsync(folderId, shared, type, ct)` |
| `ReportItemUnworn(id, folderId, shared, type, ct)` | `ReportItemUnwornAsync(id, folderId, shared, type, ct)` |
| `ReportItemAttached(folderId, shared, point, ct)` | `ReportItemAttachedAsync(folderId, shared, point, ct)` |
| `ReportItemDetached(id, primId, folderId, shared, point, ct)` | `ReportItemDetachedAsync(id, primId, folderId, shared, point, ct)` |
| `ReportSit(objectId, ct)` | `ReportSitAsync(objectId, ct)` |
| `ReportUnsit(objectId, ct)` | `ReportUnsitAsync(objectId, ct)` |

### LibreMetaverse.RLV — RlvRestrictionManager

| Before | After |
|--------|-------|
| `RemoveRestrictionsForObjects(primIds, ct)` | `RemoveRestrictionsForObjectsAsync(primIds, ct)` |

### LibreMetaverse.Voice.WebRTC — VoiceManager

| Before | After |
|--------|-------|
| `ConnectPrimaryRegion()` | `ConnectPrimaryRegionAsync()` |
| `RequestParcelVoiceInfo()` | `RequestParcelVoiceInfoAsync()` |
| `AcceptIncomingP2PCall(id)` | `AcceptIncomingP2PCallAsync(id)` |
| `JoinGroupVoice(id)` | `JoinGroupVoiceAsync(id)` |
| `LeaveGroupVoice(id)` | `LeaveGroupVoiceAsync(id)` |
| `JoinConferenceVoice(id)` | `JoinConferenceVoiceAsync(id)` |
| `LeaveConferenceVoice(id)` | `LeaveConferenceVoiceAsync(id)` |
| `StartP2PCall(id)` | `StartP2PCallAsync(id)` |
| `EndP2PCall(id)` | `EndP2PCallAsync(id)` |
| `AcceptP2PCall(id, uri, creds)` | `AcceptP2PCallAsync(id, uri, creds)` |

### LibreMetaverse.Voice.WebRTC — VoiceSession

| Before | After |
|--------|-------|
| `CreatePeerConnection(ct)` | `CreatePeerConnectionAsync(ct)` |
| `RequestProvision()` | `RequestProvisionAsync()` |
| `CloseSession()` | `CloseSessionAsync()` |

### AgentDisplayName

`AgentDisplayName` fields `ID`, `UserName`, `DisplayName`, `LegacyFirstName`, `LegacyLastName`, `IsDefaultDisplayName`, `NextUpdate`, and `Updated` are now auto-properties with `{ get; set; }`. Object initializer syntax continues to work unchanged. Direct field access (`obj.ID = x`) also continues to work.

---

## 10. `AnimationsChangedEventArgs.Animations` type narrowed

The `Animations` property on `AnimationsChangedEventArgs` changed from `LockingDictionary<UUID, int>` to `IReadOnlyDictionary<UUID, int>`. Code that called write methods (`.TryAdd`, `.TryRemove`, etc.) on the event-args value will no longer compile — subscribe to the event and use `AgentManager.SignaledAnimations` directly if you need a mutable reference.

```csharp
// Before — could mutate through event args (unsupported behavior)
e.Animations.TryAdd(animID, seqNum);

// After — read only through event args; write via the source field
client.Self.SignaledAnimations.TryAdd(animID, seqNum); // or don't write at all
```

---

## 11. `CoarseLocationUpdateEventArgs` constructor change

If you construct `CoarseLocationUpdateEventArgs` directly (e.g. in tests), the constructor now requires a `positions` parameter:

```csharp
// Before
new CoarseLocationUpdateEventArgs(sim, newEntries, removedEntries)

// After
new CoarseLocationUpdateEventArgs(sim, positions, newEntries, removedEntries)
// where positions is IReadOnlyDictionary<UUID, Vector3>
```

---

## 12. `GridClient` manager fields are now properties

All manager references on `GridClient` (`Network`, `Settings`, `Inventory`, etc.) are now `{ get; private set; }` auto-properties instead of plain public fields. `HttpCapsClient` is the exception and remains `{ get; set; }` to allow subclass swapping (as used by `FakeGridClient`).

**Read access is source-compatible** — `client.Network`, `client.Inventory`, and all other managers are accessed with identical syntax. The only code that will fail to compile is the rare case of taking a `ref` or `out` of a manager field:

```csharp
// Will not compile after 3.0 — cannot take a ref to a property
ref var net = ref client.Network;
```

### New: `IGridClient` interface

All subsystems are now declared on the `IGridClient` interface. Accepting `IGridClient` instead of the concrete `GridClient` in consumer code enables testing and DI without any loss of functionality:

```csharp
// Before
public class MyBot(GridClient client) { ... }

// After — accept the interface; concrete GridClient still works too
public class MyBot(IGridClient client) { ... }
```

`FakeGridClient` (included in the test helpers) extends `GridClient`, which implements `IGridClient`, so existing test code requires no changes.

### New: dependency injection extension

A `services.AddGridClient()` extension is now included in the main library:

```csharp
// Register with ASP.NET Core / generic host
builder.Services.AddGridClient(settings =>
{
    settings.UserAgent = "MyBot/1.0";
});

// Both IGridClient and GridClient resolve to the same singleton
public class MyBot(IGridClient client) { ... }
```

---

## Quick checklist

- [ ] Update all `using OpenMetaverse` → `using LibreMetaverse`
- [ ] Rename all async method calls per section 9 (30+ renames — search for the old names from the table)
- [ ] Update `Settings.*` references to use the new nested groups (`Timing`, `Connection`, `Agent`, `Packets`, `TexturePipeline`, `AssetCache`, `World`, `Parcel`)
- [ ] Replace all sync `Teleport()`/`GoHome()` calls with `await TeleportAsync()`/`await GoHomeAsync()`
- [ ] Replace callback-delegate patterns on `GetDisplayNames`, `RequestAgentProfile`, `GetAttachmentResources`, `RequestObjectMedia`, `GetParcelResources`, `SetAgentAccessAsync`, and inventory upload methods with `await` + tuple deconstruct
- [ ] Replace `GetGridRegion(name, layer, out var r)` with `var r = await GetGridRegionAsync(name, layer)` and check `r != null` / use `r.Value`
- [ ] Replace `MapItems(handle, type, layer, timeout)` with `await MapItemsAsync(handle, type, layer)`
- [ ] Replace `RequestBannedAgents(id, callback)` with `await RequestBannedAgentsAsync(id)` + `BannedAgents` event subscription
- [ ] Replace `RequestBanAction(id, action, agents, callback)` with `await RequestBanActionAsync(id, action, agents)`
- [ ] Remove any references to `GroupManager.GroupName2KeyCache`; maintain your own dictionary via `GroupNamesReply`
- [ ] Remove any references to `Simulator.AvatarPositions`; subscribe to `GridManager.CoarseLocationUpdate` and use `e.Positions`
- [ ] Remove any use of the `GestureManager` class
- [ ] Update `PlayGesture(id)` → `await PlayGestureAsync(id)`
- [ ] Audit Vector3/Quaternion mutation calls (`.Normalize()` etc.) and update to static value-returning forms
- [ ] Replace `LibreMetaverse.Rendering.Meshmerizer` NuGet reference with `LibreMetaverse.Rendering.MeshFoundry`
- [ ] Remove any use of `AsyncHelper.Sync`, `FindObjectByPath`, `GetInventoryRecursive`, `Utils.GetRunningRuntime()`
- [ ] Rewrite any `ref client.<Manager>` expressions — manager fields are now properties and cannot be passed by `ref`
- [ ] Optionally update constructor parameters from `GridClient` to `IGridClient` to take advantage of the new interface
