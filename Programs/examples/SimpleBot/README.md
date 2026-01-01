# SimpleBot

A simple interactive bot that responds to instant messages and can perform basic avatar actions.

## Features

- Responds to instant messages with commands
- Can perform avatar actions (sit, stand, dance, fly, jump)
- Responds to greetings in local chat
- Demonstrates async event handling and animations

## Usage

```bash
SimpleBot [firstname] [lastname] [password]
```

### Example

```bash
SimpleBot TestBot Resident mypassword
```

## Available Commands

Send these commands via instant message (IM) to the bot:

| Command | Description |
|---------|-------------|
| `help` | Show available commands |
| `where` | Get current location |
| `sit` | Sit on ground |
| `stand` | Stand up |
| `dance` | Start dancing |
| `fly` | Start flying |
| `walk` | Stop flying |
| `jump` | Jump once |
| `hello` / `hi` | Get a greeting |

## What You'll Learn

This example demonstrates:
- Event-driven programming with LibreMetaverse
- Handling instant messages and chat
- Avatar movement and animations
- Basic command parsing
- Async/await patterns with the library
- Using GridClient.Self methods for avatar control

## Extending the Bot

Easy ways to extend this bot:
- Add more commands (teleport, give inventory, group chat)
- Add timers for periodic actions
- Store user preferences
- Integrate with external APIs
- Add natural language processing
