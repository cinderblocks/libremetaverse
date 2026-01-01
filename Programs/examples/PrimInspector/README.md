# PrimInspector

A simple command-line tool for inspecting primitive objects in Second Life or OpenSim grids.

## Features

- Login to any grid
- Discover nearby objects
- Display detailed primitive properties
- Filter objects by name
- Shows primitive type, transform, materials, and special features (sculpt, light, flexible)

## Usage

```bash
PrimInspector [firstname] [lastname] [password] [search_term]
```

### Examples

Connect and inspect all nearby objects:
```bash
PrimInspector John Doe mypassword
```

Search for objects by name:
```bash
PrimInspector John Doe mypassword "Chair"
```

## What You'll Learn

This example demonstrates:
- Basic login and connection
- Object discovery via `ObjectsPrimitives` collections
- Requesting object properties
- Inspecting `Primitive` class properties:
  - Type detection (Box, Sphere, Sculpt, Mesh, etc.)
  - Transform data (Position, Rotation, Scale)
  - Construction parameters (PathCurve, ProfileCurve, Material)
  - Special features (Light, Flexible, Sculpt)
  - Ownership and permissions

## Output

Shows the 10 nearest objects with details including:
- Object name and IDs
- Primitive type
- Distance from avatar
- Transform (position, rotation, scale)
- Material and construction data
- Special features if present
- Owner and creator information
