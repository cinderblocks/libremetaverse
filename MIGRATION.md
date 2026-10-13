# Migration Guides

Each guide below takes you from one release to the next. If you are several releases behind, apply the guides for every step between your version and the new one, newest first.

- [3.1.x → 3.2.0](#migration-guide-31x--320): the library is split into more packages; model import, export and upload
- [3.1.7 → 3.1.8](#migration-guide-317--318-server-certificate-validation): server certificate validation
- [3.0.0 → 3.1.0](#migration-guide-300--310): SkiaSharp is no longer a dependency
- [2.6.x → 3.0](#migration-guide-26x--30)

---

# Migration Guide: 3.1.x → 3.2.0

Three things in this release can affect an application:

1. **The library is split into more packages.** Namespaces and type names do not change, so source needs few edits. `LibreMetaverse` forwards the types that moved, so an application already built against 3.1 keeps loading without a recompile, except for a few members that changed (see section 2) and the Collada and region archive types, which need a package reference.
2. **Model import has its own package** and gained glTF and OBJ loaders, exporters, rigged meshes and PBR materials.
3. **`UploadModelAsync` checks a model before it contacts the grid**, and makes levels of detail and a physics shape.

## What to do

- [ ] Recompile what you built against 3.1 if it calls any `Logger` method, uses `Settings.LogLevel` or `Settings.ResourceDir`, calls `Primitive.GetChildren`, calls `UploadModelAsync`, calls `Parcel.Update` or `Parcel.UpdateOtherCleanTime`, or reads a field of `NetworkManager.OutgoingPacket`; nothing else needs one (see section 2). The source needs no change.
- [ ] Add `LibreMetaverse.ModelFormats` if you use `ColladaLoader` or the Collada schema classes.
- [ ] Add `LibreMetaverse.Archiving` if you use `OarFile` or the tar archive classes.
- [ ] If you reference `LibreMetaverse.ModelFormats`, `LibreMetaverse.Rendering.MeshFoundry` or `LibreMetaverse.Rendering.Simple` and relied on it to bring in `LibreMetaverse` (for `GridClient` and the managers), reference `LibreMetaverse` yourself.
- [ ] Change a variable declared `Task<CreateItemFromAssetResult>` that holds the result of `UploadModelAsync` to `Task<ModelUploadResult>` (or use `var`).
- [ ] Read [Behavior changes](#3-behavior-changes) if you upload models or load Collada files.
- [ ] If you subclass `Primitive` and override `GetChildren`, do that another way.
- [ ] Rename `ParcelManager.ParcelAccessEntry` to `ParcelAccessEntry` and `ParcelManager.ParcelPrimOwners` to `ParcelPrimOwners`.
- [ ] If you pass `Settings.LogLevel` or `Settings.ResourceDir` by `ref` or `out`, use `ProcessSettings` instead.
- [ ] If you use the protocol package without the client and print packets, see [`PacketDecoder`](#packetdecoder-and-the-manager-enums).

## 1. Package layout

Much of `LibreMetaverse` needs no connection to a grid, so it is now in packages of its own that you can use without the client. If you reference `LibreMetaverse` you get `Types`, `StructuredData`, `Assets` and `Protocol` with it and need no change.

| Package | Holds | Depends on |
|---|---|---|
| `LibreMetaverse.Types` | `UUID`, vectors, quaternions, colors and the other basic types; the `LibreMetaverse.Threading` locks, events and semaphore; `ObservableDictionary`, `DisposalHelper`, `EventSubscriptionHelper`, `Repeat`, `FileHelper`, `CoordinateFrame`, `SlurlParser` | nothing |
| `LibreMetaverse.StructuredData` | LLSD and OSD | Types |
| `LibreMetaverse.Assets` *(new)* | the offline data model: asset types (`AssetTexture`, `AssetMesh`, `AssetNotecard`, `AssetPrim`, …), glTF documents, `Primitive`, `TextureEntry` and the particle and media types, inventory item classes, `Permissions`, `InventoryHelpers`, the avatar skeleton and appearance data (`LindenSkeleton`, `VisualParams`, the tree and grass definitions), `FacetedMesh` and the other rendering types, model data and its checks (`ModelPrim`, `ModelUploadChecks`, the LOD and physics generators), `BVHDecoder`, `AnimationTrack`, `AnimeshPlayer`, `Sounds`, `LegacyMaterial`, `Logger`, `ILogContext`, `ProcessSettings`, `Helpers`, `Targa` | StructuredData, Types, Imaging.Abstractions |
| `LibreMetaverse.Protocol` *(new)* | the UDP packet classes, `Packet`, `PacketDecoder`, the capability event-queue messages (`LibreMetaverse.Messages`), `TerrainCompressor`, the records and enums that were declared in the managers (groups, parcel information, friends, instant messages, mutes, grid regions and the like), and the login types (`LoginResponseData`, the new `LoginRequest`, `AccountLevelBenefits`, `LoginState`, `LastExecStatus`, `HomeInfo`, `BuddyListEntry`, `LoginException`) | Assets, StructuredData, Types |
| `LibreMetaverse.Transport` *(new)* | the UDP socket loop (`UDPBase`), `UDPPacketBuffer`, `ReliableCircuit` (sequence numbers, acknowledgements, resends and duplicate detection, with no socket and no client), `ThrottleRates` (the rates in an `AgentThrottle`), the traffic throttle and `IncomingPacketIDCollection` | Protocol, Assets, Types |
| `LibreMetaverse` | `GridClient`, `Simulator`, `NetworkManager`, `AgentThrottle` and the managers | Transport, Protocol, Assets, StructuredData, Types, Imaging.Abstractions |
| `LibreMetaverse.Archiving` *(new)* | `OarFile`, `TarArchiveReader`, `TarArchiveWriter`, `ArchiveConstants`, `AssetsArchiver`, `RegionSettings` | LibreMetaverse, Types |
| `LibreMetaverse.ModelFormats` *(new)* | `ColladaLoader` and the Collada schema classes, `GltfLoader`, `ObjLoader`, and the glTF, OBJ and Collada exporters | Assets, Types |
| `LibreMetaverse.Rendering.Simple`, `LibreMetaverse.Rendering.MeshFoundry` | renderers | Assets (no longer `LibreMetaverse`) |

Namespaces are unchanged (the archive classes are still in `LibreMetaverse.Assets`).

**Binary compatibility.** About 820 public types moved out of `LibreMetaverse.dll` into `Types`, `Assets`, `Protocol` and `Transport`. `LibreMetaverse.dll` carries a type forwarder for each, so an application compiled against 3.1 finds them where they are now. A test in the repository checks every public type of 3.1.8 against this. The types that moved to `ModelFormats` and `Archiving` (`ColladaLoader`, the Collada schema classes, `OarFile` and the archive classes) are not forwarded, because `LibreMetaverse` cannot refer to those packages; an application that uses them needs the package reference and a recompile. The members of types that stayed that are no longer binary compatible are listed in section 2. `Pfim`, `OggVorbisEncoder`, `zlib.net` and `ZLogger` follow the code that uses them, and reach `LibreMetaverse` through `Assets`.

`Archiving` is separate because few applications read or write OpenSim region archives:

```xml
<PackageReference Include="LibreMetaverse.Archiving" Version="3.2.*" />
```

`OarFile.SOGToXml2` is kept and forwards to the new `SceneObjectGroupXmlWriter.Write` in `Assets`, which is what `AssetPrim.EncodeXml` uses, so `AssetPrim` does not need the archive package.

**Not moved.** `ModelUploader`, `ModelUploadResult` and `MaterialUploadResult` stay in `LibreMetaverse`, because they upload through the client. So do `Baker` and `BakeLayer`.

### `PacketDecoder` and the manager enums

`PacketDecoder` used to print seven fields by the flag names of enums that are nested in managers (`DirectoryManager.EventFlags`, `DirFindFlags`, `ClassifiedQueryFlags`, `ClassifiedFlags`, `SearchTypeFlags` and `AgentManager.ControlFlags`). The protocol assembly cannot name them, so `PacketDecoder` has `RegisterFieldDecoder(key, decoder)` and `UnregisterFieldDecoder(key)`, and `LibreMetaverse` registers its decoders for those fields when the assembly is first used. Applications that use the client see the same output as before. An application that uses only `LibreMetaverse.Protocol` sees those fields as plain numbers unless it registers decoders of its own. A decoder registered for a key is used instead of a built-in one.

## 2. Smaller API changes

Six of these change a member in a way that breaks a binary built against 3.1, though not the source: every `Logger` method that took a `GridClient` or `Simulator`, which includes `Logger.Info("text")` and the others that leave the client out (see below), `Settings.LogLevel` and `Settings.ResourceDir`, `Primitive.GetChildren`, the return type of `UploadModelAsync`, and `Parcel.Update` and `Parcel.UpdateOtherCleanTime`, which are now extension methods of `LibreMetaverse` and are called the same way, and the fields of `NetworkManager.OutgoingPacket` (`Buffer`, `SequenceNumber`, `ResendCount`, `TickCount` and `Type`), which moved to its new base class `OutgoingDatagram` in `LibreMetaverse.Transport`; the type still has them, but a compiled reference to a field does not look in the base class. `ParcelAccessEntry` and `ParcelPrimOwners` are no longer nested in `ParcelManager`; they are top-level types in `LibreMetaverse.Protocol`, so source that names `ParcelManager.ParcelAccessEntry` or `ParcelManager.ParcelPrimOwners` needs the prefix removed. A binary needs a recompile if it uses them, `Parcel.AccessWhiteList` or `AccessBlackList`, `ParcelAccessListReplyEventArgs.AccessList` or `ParcelObjectOwnersReplyEventArgs.PrimOwners`, whose element types they are. Recompile what uses them. This was checked by running an application compiled against the 3.1.8 packages on the 3.2.0 assemblies and comparing, by reflection, the 30,808 public and protected members of every 3.1.8 type that 3.2.0 still resolves: no other member is missing for a compiled reference (a few, such as the throttle properties of `AgentThrottle`, are now inherited, which a compiled call finds). Compiling that same application against 3.2.0 fails only on the two renamed parcel types.

**`Logger` takes an `ILogContext`**, not a `GridClient` or `Simulator`. `ILogContext` says what the logger needs: a name to tag entries with (`LogClientName`) and a region name for scopes (`LogRegionName`). `GridClient` and `Simulator` both implement it, explicitly, so calls such as `Logger.Info("text", client)` and `Logger.BeginRegionScope(simulator)` compile as before and log the same things. Pass your own `ILogContext` to tag entries with something that is not a client, such as a bot's name.

The `GridClient` and `Simulator` overloads of 3.1 cannot be kept alongside, because `Logger` now lives in `LibreMetaverse.Assets`, which does not know the client. A binary built against 3.1 therefore needs a recompile for any `Logger` call, including `Logger.Info("text")`, since the client was an optional argument and the compiler passed it as null. The recompiled source is unchanged.

**`Settings.LogLevel` and `Settings.ResourceDir` are properties** over a new static class, `ProcessSettings`, which does not depend on the client. `Settings.LogLevel = LogLevel.Warning;` still works and `ProcessSettings.LogLevel` then reads `Warning`. They were fields, which only matters if you passed one by `ref` or `out`.

**`Primitive.GetChildren(GridClient)` is an extension method** (`PrimitiveClientExtensions.GetChildren`), because `Primitive` no longer refers to the client. `prim.GetChildren(client)` compiles as before. It was a `virtual` method that nothing in the library overrode; a subclass can no longer override it. A null client now throws `ArgumentNullException`, where it used to throw `NullReferenceException`.

**The UDP socket loop is in `LibreMetaverse.Transport`.** `UDPBase`, `UDPPacketBuffer` and `IncomingPacketIDCollection` moved, and are forwarded. `Simulator` still derives from `UDPBase`. The reliability of a simulator's circuit is now done by a `ReliableCircuit` inside `Simulator`, which keeps `SendPacket`, `SendPacketData`, the public `Sequence` field and the rest of its surface; `NetworkManager.OutgoingPacket` now derives from the new `OutgoingDatagram` and keeps its members. `AgentThrottle` stays, now deriving from the new `ThrottleRates`, which holds the seven rates, their limits, `Total` and the bytes of an `AgentThrottle` packet; `Set` and the constructor that takes a `GridClient` are still on `AgentThrottle`. Two things behave differently:
- `UDPBase` no longer reads `Settings`. It has the protected virtual properties `BindAddress` and `ReceiveQueueCapacity`, which `Simulator` overrides with `Settings.BindAddress` and `Settings.UdpReceiveQueueCapacity`, so those settings work as before. The placeholder endpoint of a new `UDPPacketBuffer` is now on `IPAddress.Any` whatever `Settings.BindAddress` is; it is replaced when a datagram is received.
- A datagram dropped because the receive queue is full is now reported to `OnPacketDropped` (and so counted in `SimStats.GetDroppedPackets`). It used to be dropped without being counted.

**The records and enums of the managers are in `LibreMetaverse.Protocol`.** Fifty-nine types that held no client and are not tied to one are forwarded, with their names, namespaces and values unchanged: for groups `Group`, `GroupMember`, `GroupRole`, `GroupTitle`, `GroupNotice`, `GroupProposal`, `GroupAccountSummary`, `Vote`, `ChatSessionMember` and the enums; for parcels `ParcelInfo`, `ParcelMedia`, `ParcelMediaCommand`, `ParcelAccessFlags`, `ParcelOverlayType`, `ParcelPropertiesStatus`, `AccessDeniedReason`, `ObjectReturnType`, `TerraformAction` and `TerraformBrushSize`; for friends `FriendInfo` and `FriendRights`; for the agent `InstantMessage`, `MuteEntry`, `MuteType`, `MuteFlags`, `TransactionInfo`, `ScriptPermission` and the other enums of `AgentTypes.cs`; and `GridRegion`, `GridLayer`, `ClassifiedAd`, `ProfilePick`, `AvatarGroup`, `EstateTask`, `RegionRestartSchedule`, `RegionProtocols`, `EnvironmentTrack`, `PayPriceType`, `ReportType`, `UpdateType`, `EstateAssetType`, `StatusCode` and `TransferError`. `Parcel` moved too, with `ParcelAccessEntry` and `ParcelPrimOwners`, which it needed (see section 2). The `...EventArgs` classes and the state of a transfer in progress also stay.

**The login types are in `LibreMetaverse.Protocol`, and a login server can write them.** `LoginResponseData`, `AccountLevelBenefits` and the types they use are forwarded. Reading a response is as before; what is new is the other direction, so that something other than the client can be a login server:
- `LoginResponseData.ToOSD()` writes a response that `Parse` reads back and that the viewer reads (`look_at` and `home` are strings in LLSD notation, `inventory-root` is an array of one map, `login` is the string `true`, `false` or `indeterminate`). A failure holds only its reason and message; a redirect adds where to go next.
- `LoginRequest` is the map a client posts: `ToOSD()` for a client, `FromOSD()` for a server, and `HashPassword` for the `$1$` form of the password. `NetworkManager` builds its request with it, and sends what it always has.
- `AccountLevelBenefits` has a default constructor, setters (`init`) and `ToOSD()`.

Four things behave differently, all of them fixes:
- After a failed login `NetworkManager.LoginMessage` is the message of the login server. `LoginAsync` used to replace it with "Canceled" (it cancelled its own wait on a failure and then caught that), and fired the progress event a second time.
- `AccountLevelBenefits.LargeTextureUploadCost` is read from an LLSD response. It was always the default, because the list was built with the wrong constructor. The XML-RPC path was right.
- `LoginResponseData.Parse` fills `ParseResult.ParsedData` with a copy, as its documentation says, reads `next_url`, `next_duration`, `next_method` and `next_options` from an LLSD response (it read them only from an XML-RPC one), and reads `FirstLogin` from `login-flags`.
- A seed capability that cannot be reached is asked for again after a pause (1, 2, 4, 8 and 16 seconds) and then given up, and not at once and without end. The old retry called its own handler until the stack was full, which ended the process.

**Constants and helpers that needed no connection** are available on their own types. The old names still work: `RegionDefaults.SizeX` and `SizeY` (the 256 metre default region size, also `Simulator.DefaultRegionSizeX` and `DefaultRegionSizeY`), and `InventoryHelpers` (`CreateInventoryItem`, `EncryptAssetID`, `DecryptShadowID`, `MagicId`; also the methods of the same names on `InventoryManager`, and `InventoryManager.MAGIC_ID`).

## 3. Behavior changes

### `UploadModelAsync`

It now checks the model before it contacts the grid, using `ModelUploadChecks`, and reports through `ModelUploadResult` (a `CreateItemFromAssetResult` with a `Diagnostics` list and `HasErrors`). An error means nothing was sent: `Success` is false and `Status` is `invalid_model`. Call `ModelUploadChecks.Check(prims)` yourself to tell the user sooner. The errors are what the reference viewer would not upload either, or what would have been written into the mesh wrongly:

- more than 8 faces on a prim (`TooManyFaces`);
- a face with more than 65534 vertices, which a 16-bit index cannot reach (`LimitExceeded`);
- more than 110 joints in a skin (`LimitExceeded`; a glTF mesh counts only the joints it is weighted to, not every joint of the file's skin);
- a prim with no faces, or a face with no triangles (`NoGeometry`);
- a triangle that uses a vertex the face does not have, a position, normal, texture coordinate or placement that is not a number, or a skin whose matrices or weights do not fit its joints and vertices (`InvalidModel`).

A texture whose size is not known is a warning (`TextureSizeUnknown`), because the upload goes ahead and the fee is quoted from the whole texture. Every other status (`capability_missing`, `cost_rejected`, the grid's own states) is as it was.

`UploadModelAsync` used to send a prim with more than 8 faces, or a triangle that used a vertex the face did not have, and leave the grid to refuse it (an index past 65535 was cut to 16 bits without a word). All of these are now refused first, with a code. The loaders only warn about more than 8 faces and return the prim; it is the upload that refuses.

The return type is now `Task<ModelUploadResult>`, which is source compatible with code that assigns it to a variable or to a `CreateItemFromAssetResult`; a variable declared as `Task<CreateItemFromAssetResult>` needs changing.

It also makes the lower levels of detail and a physics shape now (`ModelAssetOptions`, an optional last argument; leave it out for the defaults). The medium, low and lowest levels have a third, a ninth and a twenty-seventh of the triangles of the one above, the reference viewer's own default, made by collapsing edges cheapest first. A vertex that comes out of it is a vertex that went in, so texture coordinates, normals and the weights of a rigged mesh are exact, each face keeps its place and material and its last triangle, the border of a face stays where it was, and a collapse that would flip, fold or pinch the surface is refused. The physics shape is the convex hull of the mesh (at most 255 points), in place of the fixed box; a rigged mesh, or a mesh too flat to have a hull, keeps the box. `ModelAssetOptions.None` gives what it sent before, `LodReduction` changes the factor and `GenerateLods` and `GeneratePhysics` turn each off. `ModelPrim.CreateAsset(UUID)` is unchanged; `CreateAsset(UUID, ModelAssetOptions)` makes the same asset as an upload would.

An uploaded mesh therefore no longer looks the same at every distance. Its far levels are simplified, which is also what lowers its land impact, and its physics follows its shape and not a box. If a model looks wrong when seen from far away, or its physics is too tight, set `GenerateLods` or `GeneratePhysics` to false to send what was sent before. Not made: a convex decomposition of the shape (the hull is one piece, so a hollow shape is filled), a physics mesh, levels of detail you supply yourself, and an estimate of the land impact. The viewer builds its base hull from the low level and this uses the full one, so it can be a little larger.

### `ColladaLoader`

- **Y-up files are turned upright.** It used to turn the top of a `Y_UP` file towards -Z, so such models came out upside down (and facing the other way). Files with `Z_UP` (what Blender's Collada export writes) are unaffected; a `Y_UP` file that you compensated for by hand will now be rotated twice.
- **Array parsing.** White space before the first value or after the last of a `float_array` or `Name_array` was read as values of their own, so a pretty-printed array was read shifted by one. It is ignored now. A mesh with a position that is not a number is refused, rather than loading with every vertex scaled by the bad one.
- **Normals** are stored the way the viewer does when it fits a model into the unit cube: multiplied by the model's size along each axis and renormalized, which the viewer's drawing undoes again. Before, a mesh that was not the same size in every direction was shaded slightly wrong, with its normals leaning away from the long sides. Meshes of equal size in all directions are unaffected. The new loaders do the same.
- **Skins are read.** A node that instances a controller was ignored; it now becomes a rigged prim (see [Rigged meshes](#rigged-meshes)), which `UploadModelAsync` uploads as a rigged mesh.

### `CoordinateFrame`

`CoordinateFrame` kept the second axis at the wrong angle after a turn that was not a multiple of 45 degrees: the step that squares the axes took a component-wise product of two vectors where a dot product was meant. A frame now stays square after `Rotate`, `Roll`, `Pitch` and `Yaw`. If you compensated for the drift, remove that. `CoordinateFrame`, `SlurlParser`, `Sounds` and `LegacyMaterial` are forwarded from their old assembly like the other moved types.

### Region archives and linkset XML

`OarFile` now reads region archives that OpenSim wrote. Before, the first object in such an archive stopped the load (`Element 'TaskInventory' … was not found`), so no objects came back, and the region settings failed with `There is no Unicode byte order mark`. Reading is now by element name, in any order, with the elements OpenSim leaves out when they are empty (such as `TaskInventory`) treated as absent, and it also accepts the `RootPart` layout of later OpenSim versions. An object file that cannot be read is logged and skipped, and the rest of the archive still loads. The same reader is behind `AssetPrim.DecodeXml` and `AssetPrim.LoadPrim`, and `RegionSettings.FromStream` is order independent too.

Two results differ from what you may have been getting:

- **Hover text color.** OpenSim writes each component as a byte, 0 to 255; a value with a decimal point is read as a fraction, 0 to 1. `AssetPrim.EncodeXml` now writes bytes, as OpenSim does, and writes the alpha component from alpha (it wrote the green value). Hover text that was read as black or white from an OpenSim archive now has its real color.
- **Sculpt, flexible and light prims keep their settings.** `AssetPrim.EncodeXml`, and so `OarFile.SavePrimsAsync`, wrote none of these, so a sculpted prim came back from an exported archive as a plain one. It now writes the sculpt texture and type, the flexible settings and the light settings, in the element order OpenSim uses. Particle systems are written and read as the bytes the simulator sends viewers, in base64, and a system OpenSim wrote is written back byte for byte. Texture animations (`llSetTextureAnim`) are read and written the same way, as the 16 bytes viewers are sent: `PrimObject` has a new `TextureAnimation` field, null for a prim without one, which `FromPrimitive` and `ToPrimitive` copy. The collision sound and its volume, which were read but not written, are written too.
- **Archive versions.** Every region archive says which version of the format it is in `archive.xml` (OpenSim has written `0.8` since 2012; a `1.0` format with several regions in one archive exists). `OarFile` used to ignore it. `OarFile.ReadArchiveInfo(path)` and a new `UnpackageArchive` overload with an `ArchiveInfoLoadedCallback` now give an `ArchiveInfo`: the version, when the archive was made, its ID, the region size, whether it is a megaregion and whether it includes assets. An archive with no readable control file still loads, and one of a newer version than `ArchiveInfo.SupportedMajorVersion`.`SupportedMinorVersion` (0.8) is logged and read as that version; multi-region archives are not read. `OarFile.PackageArchive` now writes a full control file, version `0.8`, with the time, an ID, a 256 by 256 region and whether assets are included, where it wrote `0.1` and nothing else. Pass an `ArchiveInfo` to `PackageArchive` to set these yourself. The existing method signatures are unchanged.
- **Particle systems on `PrimObject`.** `PrimObject.FromPrimitive` and `ToPrimitive` never copied the burst radius, and mixed up the age of the source (`MaxAge`) and the age of a particle (`PartMaxAge`). They now go through `ParticlesBlock.FromParticleSystem` and `ToParticleSystem`, which copy each field to its own. `ParticlesBlock` gained `Crc`, `ParticleStartGlow`, `ParticleEndGlow`, `BlendFuncSource` and `BlendFuncDest`, so that glow and blend functions are not lost, and `IsSet`.
- **Task inventory permissions.** `ItemBlock.PermsBase` is the item's base permissions. It used to end up as the `PermsMask` element, which is what a script was granted.

## 4. Model import, export and materials

### Loaders

`ColladaLoader` and the Collada 1.4 schema classes (`LibreMetaverse.ImportExport.Collada14`) ship in `LibreMetaverse.ModelFormats` instead of `LibreMetaverse`. Namespaces are unchanged, so adding a reference to the package is all that source code needs:

```xml
<PackageReference Include="LibreMetaverse.ModelFormats" Version="3.2.*" />
```

`ModelPrim`, `ModelFace` and `ModelMaterial` are in `LibreMetaverse.Assets`, and `ModelUploader` stays in `LibreMetaverse`. Loaders implement the new `IModelLoader` (`List<ModelPrim> Load(string filename, bool loadImages)`), which is the extension point for other 3D formats. The package also adds `GltfLoader`, which reads glTF 2.0 `.gltf` and `.glb` files, and `ObjLoader`, which reads Wavefront `.obj` files with their `.mtl` material libraries:

```csharp
IModelLoader loader = Path.GetExtension(path).ToLowerInvariant() switch
{
    ".dae" => new ColladaLoader(new SkiaTextureCodec()),
    ".gltf" or ".glb" => new GltfLoader(new SkiaTextureCodec()),
    ".obj" => new ObjLoader(new SkiaTextureCodec()),
    _ => throw new NotSupportedException()
};
List<ModelPrim> prims = loader.Load(path, loadImages: true);
```

`ObjLoader` turns each `o` object into a prim (or each `g` group, in a file with no objects) and each material it uses into a face. It reads the diffuse color, opacity and diffuse texture of each material, and fan-triangulates polygons, which is only right for convex ones. Points, lines and curves are ignored. OBJ does not say which way is up, so Y-up is assumed, as most exporters write it; set `YUp = false` for a Z-up model. All three loaders put a Y-up model's top along +Z.

`GltfLoader` reads triangle meshes, each material's base color factor and base color texture, the node hierarchy (instanced meshes become one prim per node) and skins. Morph targets, animation, other primitive modes and Draco/meshopt/quantized meshes are not supported. Like `ColladaLoader`, it only reads buffers and textures from the model's directory unless `RestrictTexturesToModelDirectory` is set to false. PNG and JPEG textures, which glTF models normally use, need an `ITextureCodec` such as `SkiaTextureCodec`.

### Diagnostics

The loaders can say what they left out. `GltfLoader`, `ObjLoader` and `ColladaLoader` implement `IDiagnosticModelLoader`, whose `LoadWithDiagnostics` gives the prims along with a list of `ModelDiagnostic` entries, and `Load` is the same call with the diagnostics thrown away (they are also written to the log, once each):

```csharp
ModelLoadResult result = loader.LoadWithDiagnostics(path, loadImages: true);
if (result.HasErrors)                                  // nothing usable came out; the diagnostic says why
    Show(result.Diagnostics.First().Code);             // FileUnreadable, FileMalformed, FileUnsupported, LimitExceeded, LoaderFailed
foreach (var d in result.Diagnostics)
    if (d.Code == ModelDiagnosticCode.TextureOutsideModelDirectory)
        Offer("Allow textures from other folders?");   // RestrictTexturesToModelDirectory
```

Switch on `Code`, not on the message. `Subject` is text from the file (an object, joint or texture name), and `Count` is how many times a counted thing happened (skipped faces, vertices without a weight) so there is one entry for them, not one each. A bad texture or material library is a warning, because it costs only that; an error means the file gave nothing. `GltfExporter`, `ObjExporter` and `ColladaExporter` have a `Diagnostics` property that holds what the last `Build` or `Save` left out, and starts empty each time.

### Rigged meshes

A skinned mesh becomes a rigged prim: its `ModelPrim.Skin` and the per-vertex `ModelFace.Weights` are written into the mesh asset by `CreateAsset`, so `UploadModelAsync` uploads it as a rigged mesh.

**glTF.** Joints are matched by node name against the avatar skeleton (`mPelvis`, `mTorso`, …, collision volumes, and the skeleton's aliases); joints it does not have are dropped, and so are influences beyond a vertex's strongest four. By default only each joint's position is taken from the file and its axes are the avatar's (`UseAvatarJointAxes`), because the avatar's bones have no rotation while a rig from a modelling program such as Blender usually gives its bones axes of their own, which would turn the mesh around each joint on the avatar; set it to false to carry the inverse bind matrices over exactly as authored. Joint position overrides are not imported. A skinned mesh is placed by its joints, so its node's transform is ignored, as glTF says.

**Collada.** Read the way the viewer reads it: the joint names come from the skin's `JOINT` source (through the avatar's aliases), the inverse bind matrices and bind shape matrix are taken as they are in the file, and only the four strongest weights of a vertex are kept. By default (`UseAvatarJointAxes`) each joint keeps the position and size its inverse bind matrix gives it and takes the rotation the avatar's joint has there. A rig made for Second Life has those already, at whatever shape it was bound (a collision volume's scale follows the wearer's shape), and does not notice; a rig from a modelling program with bones turned about axes of their own needs it. Set it to false to carry the matrices over exactly, as the viewer's uploader does. The file's up axis and unit are not applied to a rig, as in the viewer, and a warning says so when the file is not Z-up in meters. Joint position overrides are not imported, a skin with no joint of the avatar loads as an ordinary mesh, and morph targets are not supported.

### Materials

`GltfLoader` also reads the physically based material of each face. `ModelMaterial.Pbr` (null for OBJ and COLLADA) holds the factors exactly as the file has them (nothing clamped), the alpha mode and cutoff, double sided, the normal scale and occlusion strength, and a `ModelTexture` for each of the base color, normal, metallic-roughness and emissive maps with its name, texture coordinate set and `KHR_texture_transform`. A mesh upload cannot carry any of this (a mesh face has a color and one texture), so it is for making a Second Life material: `Pbr.ToAssetMaterial(texture => idYouGotByUploadingIt)` gives the `AssetMaterial`. Like the viewer's own import, it leaves double sided off whatever the file says, and leaves the texture transforms out unless you pass `carryTextureTransforms: true` (the viewer does not read them, and which way up they are in a Second Life material has not been checked against it).

`InventoryManager.CreateMaterialAsync` puts a material in inventory the way the viewer's material editor does for a new one (checked against `LLMaterialEditor::saveIfNeeded` and `LLObjectsMaterialItemCallback`): the textures are uploaded first and the new asset IDs put in the material, an empty material item is made in the Materials folder, its permissions are set (the message that makes an item carries only the next owner's), and the material is uploaded to it with `UpdateMaterialAgentInventory`. Give it a `ModelPbrMaterial` and it does all of it, or an `AssetMaterial` whose textures you have uploaded yourself and it does the last three steps:

```csharp
var loader = new GltfLoader(codec) { LoadPbrTextures = true };
var material = loader.Load(path, loadImages: true)[0].Faces[0].Material.Pbr!;
var result = await client.Inventory.CreateMaterialAsync(material, permissions, confirmCost: fee => Ask(fee));
// result.ItemID, result.AssetID, and result.Textures: the asset ID of each texture that was uploaded
```

Everything that can be found out before paying is: a missing capability, or a refused fee, uploads nothing. Each texture costs the usual upload fee and stays in inventory if a later step fails (`result.Textures` says which), as does an empty material item if the final upload fails (`result.ItemID`). Pass the same `uploadedTextures` dictionary to make several materials from one model and a texture they share is uploaded once. A texture that was never loaded is left out and reported in `result.Diagnostics`.

The textures themselves are loaded only if you set `GltfLoader.LoadPbrTextures` (off by default, because it decodes and compresses extra images on every load and a mesh upload ignores them) and ask for images. They are prepared as the viewer prepares a glTF material's: the occlusion map goes into the red channel of the metallic-roughness map (scaled to its size), a metallic-roughness map with no occlusion gets a white red channel (glTF ignores that channel and files often leave it black, which would make the surface black), an occlusion map with no metallic-roughness map becomes a white one with the occlusion in red, alpha is dropped from all but the base color, and the normal map is compressed without loss. Every image goes through the same checks as the base color (`RestrictTexturesToModelDirectory`, file size limits, diagnostics). The encoder this library uses writes an alpha component to every texture, so "no alpha" means an opaque one. With the option on, `PbrApproximated` reports what a Second Life material cannot hold: a second set of texture coordinates, a normal scale or occlusion strength other than 1, and `KHR_materials_*` extensions.

### Exporters

`GltfExporter` writes decoded `FacetedMesh` objects (downloaded meshes, or prims tessellated by a renderer) as a `.glb` or a self-contained `.gltf`:

```csharp
var exporter = new GltfExporter
{
    // glTF cannot hold JPEG2000, so hand back PNG or JPEG bytes; return null to leave a face untextured
    ImageProvider = textureId => LoadAsPng(textureId) is { } png ? new ExportImage(png, "image/png") : null
};
foreach (var mesh in linkset) exporter.Add(mesh);   // root and children together
exporter.Save("model.glb");
```

A linkset keeps its layout (the root's rotation, each prim's own position, rotation and size), and the root's world position is dropped. A rigged mesh becomes a glTF skin: the joints are arranged as the default avatar skeleton but rest where the mesh's own bind pose puts them, and every vertex keeps its weights. The rig is turned to Y-up the way Blender's glTF exporter does it, in the vertices, joint transforms and inverse bind matrices themselves, so `GltfLoader` reads it back to the same skin. Face color, opacity and texture are exported; texture repeats, offsets and rotation, glow, shininess, bump maps and full bright are not (colors are written unconverted), and neither are a rigged mesh's pelvis offset, alternate inverse bind matrices and lock-scale flag, which glTF cannot express.

`ObjExporter` and `ColladaExporter` take the same meshes and the same `ImageProvider`, and lay a linkset out the same way. They write several files, because both formats refer to their textures by file name, so `Save("house.obj")` also writes `house.mtl` and a PNG or JPEG named after each texture's asset ID next to it. `Build("house.obj")` returns the same files as byte arrays (`ExportedFile`) if you want to put them somewhere else. The OBJ is Y-up with every prim already in place, as OBJ has no transforms, and the COLLADA is Z-up with one node per prim. Neither can hold a skin, so a rigged mesh is written as it stands in the pose its bind shape matrix gives it, and the skin is dropped. A COLLADA material has the texture or the color, not both, because the format allows one for the diffuse. Texture repeats, offsets and rotation are not applied (call the renderer's `TransformTexCoords` on a copy first if you want them baked in). `SkipTransparentFaces` leaves out faces with an alpha under 1/100, as Radegast's export does.

---

# Migration Guide: 3.1.7 → 3.1.8: Server certificate validation

Server certificates are now checked. Previously every certificate was accepted, which let anyone
on the network path impersonate a grid and read login credentials. Second Life's servers present
certificates that verify normally, so nothing changes there. A grid whose certificate does not
verify (self-signed, private CA, or a platform with an incomplete trust store) is now refused, and
the warning in the log shows the certificate's SHA-256 fingerprint. Configure `Settings.Security`:

```csharp
// Trust one specific certificate (the way to connect to a self-signed server)
client.Settings.Security.TrustCertificate("D1:E0:FE:13:...");

// Trust the roots in a PEM file, like the viewer's ca-bundle.crt
client.Settings.Security.CaBundlePath = "/path/to/ca-bundle.pem";

// Decide per connection, for example by asking the user
client.Settings.Security.CertificateValidationCallback = (host, cert, errors) => AskUser(host, cert);

// Restore the old behaviour (not recommended)
client.Settings.Security.VerifyServerCertificates = false;
```

---

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
