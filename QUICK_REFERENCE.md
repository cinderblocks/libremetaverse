# LibreMetaverse Quick Reference

Quick examples for common tasks with LibreMetaverse.

## Table of Contents
- [Dependency Injection](#dependency-injection)
- [Connection & Login](#connection--login)
- [Object Inspection](#object-inspection)
- [Avatar Control](#avatar-control)
- [Inventory](#inventory)
- [Chat & Messaging](#chat--messaging)
- [Textures & Assets](#textures--assets)
- [OSD Serialization](#osd-serialization)
- [Voice (WebRTC)](#voice-webrtc)

## Dependency Injection

`GridClient` implements `IGridClient` and can be registered as a singleton in any `IServiceCollection`-based container (ASP.NET Core, .NET Generic Host, etc.).

### Register and inject

```csharp
// Program.cs / Startup.cs
builder.Services.AddGridClient(settings =>
{
    settings.UserAgent = "MyBot/1.0";
    settings.Timing.LoginTimeout = 30_000;
});

// Consumer — inject IGridClient (or GridClient directly, both resolve to the same singleton)
public class BotService(IGridClient client)
{
    public async Task RunAsync()
    {
        var loginParams = client.Network.DefaultLoginParams(
            "FirstName", "LastName", "password", "MyBot", "1.0");
        await client.Network.LoginAsync(loginParams);
    }
}
```

### Manual construction (no DI container)

```csharp
// Works exactly as before
var client = new GridClient();
```

### Testing with FakeGridClient

`FakeGridClient` extends `GridClient` (which implements `IGridClient`), so it works wherever `IGridClient` is accepted:

```csharp
IGridClient client = new FakeGridClient();
client.AddHttpResponse(capsUri, HttpStatusCode.OK, responseBody);
// pass client to the code under test
```

---

## Connection & Login

### Configuring the Client
```csharp
var client = new GridClient();

// Timing
client.Settings.Timing.TeleportTimeout = 60_000;   // ms
client.Settings.Timing.LoginTimeout    = 30_000;

// Connection
client.Settings.Connection.LoginServer = Settings.AgniLoginServer; // default
client.Settings.Connection.MfaEnabled  = false;

// Agent
client.Settings.Agent.SendUpdates = true;

// Texture pipeline
client.Settings.TexturePipeline.MaxConcurrentDownloads = 6;

// Process-wide (static)
Settings.MaxHttpConnections = 64;
Settings.UserAgent = "MyApp/1.0";
```

### Basic Login
```csharp
var client = new GridClient();
var loginParams = client.Network.DefaultLoginParams(
    "FirstName", "LastName", "password", 
    "MyApp", "1.0.0");

var success = await client.Network.LoginAsync(loginParams);
if (success)
{
    Console.WriteLine($"Logged in to {client.Network.CurrentSim.Name}");
}
```

### Login with Timeout
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
try
{
    var success = await client.Network.LoginAsync(loginParams, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Login timed out");
}
```

### Login to Specific Grid
```csharp
loginParams.URI = "https://grid.example.com/login";
var success = await client.Network.LoginAsync(loginParams);
```

## Object Inspection

### Get Nearby Objects
```csharp
var objects = client.Network.CurrentSim.ObjectsPrimitives.Values;
foreach (var obj in objects)
{
    Console.WriteLine($"{obj.Properties?.Name ?? "Unknown"} - {obj.Type}");
}
```

### Find Objects by Name
```csharp
var found = client.Network.CurrentSim.ObjectsPrimitives.Values
    .Where(p => p.Properties?.Name?.Contains("Chair") ?? false)
    .ToList();
```

### Request Object Properties
```csharp
client.Objects.ObjectProperties += (sender, e) =>
{
    Console.WriteLine($"Got properties for {e.Properties.Name}");
};

client.Objects.SelectObject(client.Network.CurrentSim, prim.LocalID);
```

### Inspect Primitive Details
```csharp
Console.WriteLine($"Type: {prim.Type}");
Console.WriteLine($"Position: {prim.Position}");
Console.WriteLine($"Scale: {prim.Scale}");
Console.WriteLine($"Material: {prim.PrimData.Material}");

if (prim.Sculpt != null)
    Console.WriteLine($"Sculpt: {prim.Sculpt.Type}");

if (prim.Light != null)
    Console.WriteLine($"Light: {prim.Light.Intensity}");
```

## Avatar Control

### Movement
```csharp
// Walk forward
client.Self.Movement.AtPos = true;
client.Self.Movement.SendUpdate();

// Stop
client.Self.Movement.AtPos = false;
client.Self.Movement.SendUpdate();

// Turn
client.Self.Movement.TurnLeft = true;
client.Self.Movement.SendUpdate();
```

### Flying
```csharp
client.Self.Fly(true);  // Start flying
client.Self.Fly(false); // Stop flying
```

### Sitting
```csharp
client.Self.SitOnGround();
client.Self.RequestSit(targetObject, Vector3.Zero);
client.Self.Stand();
```

### Animations
```csharp
client.Self.AnimationStart(Animations.DANCE1, true);
client.Self.AnimationStop(Animations.DANCE1, true);
```

### Teleport
```csharp
bool success = await client.Self.TeleportAsync("Region Name", new Vector3(128, 128, 25));
if (success)
    Console.WriteLine("Teleported successfully");
```

## Inventory

### Access Root Folder
```csharp
var root = client.Inventory.Store.RootFolder;
var contents = client.Inventory.Store.GetContents(root);
```

### Search Inventory
```csharp
var results = client.Inventory.Store.Items.Values
    .Where(i => i.Name.Contains("sword", StringComparison.OrdinalIgnoreCase))
    .ToList();
```

### Find by Type
```csharp
var textures = client.Inventory.Store.Items.Values
    .Where(i => i.AssetType == AssetType.Texture)
    .ToList();
```

### Navigate Folders
```csharp
void PrintFolder(InventoryFolder folder, int depth)
{
    var indent = new string(' ', depth * 2);
    var contents = client.Inventory.Store.GetContents(folder);
    
    foreach (var item in contents)
    {
        if (item is InventoryFolder subfolder)
        {
            Console.WriteLine($"{indent}[Folder] {subfolder.Name}");
            PrintFolder(subfolder, depth + 1);
        }
        else
        {
            Console.WriteLine($"{indent}{item.Name}");
        }
    }
}
```

## Chat & Messaging

### Local Chat
```csharp
client.Self.Chat("Hello, world!", 0, ChatType.Normal);
```

### Shout
```csharp
client.Self.Chat("Important message!", 0, ChatType.Shout);
```

### Whisper
```csharp
client.Self.Chat("Quiet message", 0, ChatType.Whisper);
```

### Receive Chat
```csharp
client.Self.ChatFromSimulator += (sender, e) =>
{
    if (e.SourceID != client.Self.AgentID)
        Console.WriteLine($"{e.FromName}: {e.Message}");
};
```

### Instant Messages
```csharp
// Send IM
client.Self.InstantMessage(targetUUID, "Hello!");

// Receive IMs
client.Self.IM += (sender, e) =>
{
    Console.WriteLine($"IM from {e.IM.FromAgentName}: {e.IM.Message}");
    
    // Reply
    client.Self.InstantMessage(e.IM.FromAgentID, "Thanks for the message!");
};
```

### Group Chat
```csharp
client.Self.InstantMessageGroup(groupUUID, "Hello group!");
```

## Textures & Assets

### Download Texture
```csharp
var texture = await client.Assets.RequestImageAsync(textureUUID);
if (texture != null)
    File.WriteAllBytes("texture.jp2", texture.AssetData);
```

### Upload Asset
```csharp
var data = File.ReadAllBytes("image.jp2"); // must already be JPEG2000
var assetID = await client.Assets.RequestUploadAsync(
    AssetType.Texture, data, storeLocal: false, UUID.Random());
```

### Request Asset
```csharp
var asset = await client.Assets.RequestAssetAsync(assetUUID, AssetType.Notecard, priority: true);
if (asset != null)
    Console.WriteLine(Utils.BytesToString(asset.AssetData));
```

## OSD Serialization

### Serialize Primitive to OSD
```csharp
var prim = new Primitive { /* ... */ };
var osd = prim.GetOSD();
var json = OSDParser.SerializeJsonString(osd, true);
File.WriteAllText("prim.json", json);
```

### Deserialize OSD to Primitive
```csharp
var json = File.ReadAllText("prim.json");
var osd = OSDParser.DeserializeJson(json);
var prim = Primitive.FromOSD(osd);
```

### Convert OSD Formats
```csharp
// JSON to XML
var json = File.ReadAllText("data.json");
var osd = OSDParser.DeserializeJson(json);
var xml = OSDParser.SerializeLLSDXmlString(osd);
File.WriteAllText("data.xml", xml);

// XML to Binary
var osd = OSDParser.DeserializeLLSDXml(xmlString);
var binary = OSDParser.SerializeLLSDBinary(osd);
File.WriteAllBytes("data.bin", binary);
```

### Working with OSD Maps
```csharp
var map = new OSDMap
{
    ["name"] = "Test Object",
    ["position"] = OSD.FromVector3(new Vector3(128, 128, 25)),
    ["active"] = true
};

// Read values
var name = map["name"].AsString();
var pos = ((OSDArray)map["position"]).AsVector3();
var active = map["active"].AsBoolean();
```

## Event Handling

### Subscribe to Events
```csharp
client.Network.LoginProgress += (sender, e) =>
{
    Console.WriteLine($"Login: {e.Status}");
};

client.Network.Disconnected += (sender, e) =>
{
    Console.WriteLine($"Disconnected: {e.Reason}");
};

client.Objects.ObjectUpdate += (sender, e) =>
{
    Console.WriteLine($"Object update: {e.Prim.ID}");
};
```

### Unsubscribe from Events
```csharp
void MyHandler(object sender, EventArgs e) { }

client.Network.LoginProgress += MyHandler;
// Later...
client.Network.LoginProgress -= MyHandler;
```

## Error Handling

### Graceful Disconnection
```csharp
if (client.Network.Connected)
    await client.Network.LogoutAsync();
```

### Cancellation and Timeouts
```csharp
// Cancel after 10 seconds
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    bool success = await client.Self.TeleportAsync("Destination", new Vector3(128, 128, 25), cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation timed out or was cancelled");
}
```

## Voice (WebRTC)

Requires the `LibreMetaverse.Voice.WebRTC` package (.NET 8+).

### Connect spatial voice

```csharp
var voiceManager = new VoiceManager(client);

voiceManager.PeerConnectionReady  += () => Console.WriteLine("Voice connected");
voiceManager.PeerConnectionClosed += () => Console.WriteLine("Voice disconnected");
voiceManager.PeerJoined  += id => Console.WriteLine($"Peer joined: {id}");
voiceManager.PeerLeft    += id => Console.WriteLine($"Peer left: {id}");

await voiceManager.ConnectPrimaryRegion();
```

### Enable audio processing (noise suppression, HPF)

Call after constructing `VoiceManager`. Processing is enabled for the lifetime of the
audio device and survives reconnects.

```csharp
// Recommended defaults: noise suppression + high-pass filter
voiceManager.AudioDevice.EnableAudioProcessing();

// With AGC and echo cancellation
voiceManager.AudioDevice.EnableAudioProcessing(
    noiseSuppression: true,
    highPassFilter:   true,
    agc:              true,
    echoCancellation: true);  // requires speaker output as AEC reference (wired automatically)

// Turn off
voiceManager.AudioDevice.DisableAudioProcessing();
```

### Mic mute / unmute

```csharp
voiceManager.AudioDevice.MicMute = true;   // mute
voiceManager.AudioDevice.MicMute = false;  // unmute
```

### Speaker and microphone volume

```csharp
voiceManager.AudioDevice.SpeakerLevel = 0.8f;  // 0.0–1.0
```

### Disconnect voice

```csharp
voiceManager.Disconnect();
```

## More Examples

For complete working examples, see:
- [Programs/examples/](Programs/examples/) - Interactive applications
- [Programs/tools/](Programs/tools/) - Command-line utilities
- [LibreMetaverse.Tests/](LibreMetaverse.Tests/) - Unit tests with usage examples
