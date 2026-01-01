# LibreMetaverse Examples

This directory contains example applications demonstrating various features of the LibreMetaverse library.

## Getting Started Examples

Perfect for learning the basics:

### 1. **SimpleBot** - Interactive Bot
A simple bot that responds to instant messages and can perform basic avatar actions.

**Features:**
- Instant message handling
- Avatar actions (sit, stand, dance, fly, jump)
- Chat responses
- Command system

**Learn:** Event handling, avatar control, async patterns

[?? Read more](SimpleBot/README.md)

---

### 2. **PrimInspector** - Object Inspector
Inspect primitive objects in the world and view their properties.

**Features:**
- Object discovery
- Detailed primitive properties
- Search by name
- Distance-based filtering

**Learn:** Object system, Primitive class, property inspection

[?? Read more](PrimInspector/README.md)

---

### 3. **InventoryExplorer** - Inventory Tool
Explore, search, and export inventory data.

**Features:**
- Inventory navigation
- Search and filtering
- Statistics
- Export to text file

**Learn:** Inventory system, asset types, tree traversal

[?? Read more](InventoryExplorer/README.md)

---

## Advanced Examples

### **PacketDump** - Network Packet Logger
Logs all network packets received from the simulator.

**Learn:** Low-level network protocol, packet handling

---

### **IRCGateway** - IRC Bridge
Bridges Second Life chat with IRC channels.

**Learn:** Multi-protocol integration, chat systems

---

### **TestClient** - Full-Featured Client
A comprehensive command-line client with many features.

**Learn:** Complete client implementation, advanced features

---

## Building the Examples

All examples target .NET 8.0 and .NET 9.0 and can be built with:

```bash
# Build a specific example
dotnet build PrimInspector/PrimInspector.csproj

# Or build all examples at once from solution root
dotnet build
```

## Running Examples

After building, navigate to the output directory:

```bash
cd PrimInspector/bin/Release/net8.0/
dotnet PrimInspector.dll [arguments]
```

Or run directly from the project directory:

```bash
cd PrimInspector
dotnet run -- [arguments]
```

## Common Patterns

All examples demonstrate:
- ? Async/await login
- ? Event-driven architecture
- ? Proper error handling
- ? Clean resource disposal
- ? Timeout handling

## Security Note

?? **Never hardcode credentials in your applications.** All examples accept credentials as command-line arguments for demonstration purposes only. In production:
- Use secure credential storage
- Consider OAuth or token-based auth where available
- Never commit credentials to source control

## Contributing Examples

Want to add an example? Great! Please ensure:
1. Clear README explaining the example
2. Well-commented code
3. Targets .NET 8.0/9.0
4. Follows existing patterns
5. Demonstrates one or two concepts clearly

## Need Help?

- Check individual example READMEs
- Review the [main documentation](../../../README.md)
- Visit the [GitHub repository](https://github.com/cinderblocks/libremetaverse)
- Ask in the LibreMetaverse community

## Example Ideas

Looking to contribute? Here are some example ideas:
- Group chat manager
- Teleport utility
- Friend list manager
- Estate management tool
- Texture downloader
- Script compiler
- Voice chat demo
- Region monitoring tool
