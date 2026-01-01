# InventoryExplorer

A command-line tool for exploring, searching, and exporting inventory data from Second Life or OpenSim.

## Features

- Download and display inventory structure
- Search for items by name and type
- Show inventory statistics
- Export inventory tree to text file
- Filter by asset type

## Usage

```bash
InventoryExplorer [firstname] [lastname] [password] [options]
```

## Options

| Option | Description |
|--------|-------------|
| `--stats` | Display inventory statistics |
| `--search <term>` | Search for items by name |
| `--type <type>` | Filter by asset type (Texture, Object, Notecard, etc.) |
| `--export <file>` | Export full inventory tree to text file |

## Examples

Show inventory statistics:
```bash
InventoryExplorer John Doe password123 --stats
```

Search for items:
```bash
InventoryExplorer John Doe password123 --search "sword"
```

Search for specific type:
```bash
InventoryExplorer John Doe password123 --search "landscape" --type Texture
```

Export inventory to file:
```bash
InventoryExplorer John Doe password123 --export my_inventory.txt
```

Browse top-level folders:
```bash
InventoryExplorer John Doe password123
```

## What You'll Learn

This example demonstrates:
- Working with the inventory system
- Navigating inventory folders and items
- Searching and filtering inventory
- Using `InventoryManager` and `InventoryStore`
- Asset type classification
- Tree traversal and recursion
- File export functionality

## Asset Types

Common asset types you can filter by:
- `Texture`
- `Sound`
- `Object`
- `Notecard`
- `LSLText` (scripts)
- `Animation`
- `Gesture`
- `Clothing`
- `Bodypart`

## Output Format

Statistics show:
- Total item count
- Top item types by count
- Folder structure summary

Export creates a hierarchical text file showing:
- Folder hierarchy
- Item names and types
- UUIDs for reference
