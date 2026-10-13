```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.PacketDump

A command-line tool that logs in to Second Life or an OpenSimulator grid and prints the type of every UDP packet the simulator sends, one per line, for as long as you tell it to. It is for seeing what a grid actually says: which messages a region sends on arrival, in what order, and which ones a feature produces.

```
  LMV.PacketDump  ──►  LibreMetaverse (login, circuit)  ──►  each packet's type, one per line
```

## Install

```
dotnet tool install --global LibreMetaverse.PacketDump
```

## Use

```
LMV.PacketDump <first name> <last name> <password> <seconds>
```

`seconds` is how long to stay logged in; `0` means until you stop the tool. Each received packet is logged by its type name. The tool turns the land, wind and cloud throttles all the way down so the terrain and weather traffic does not drown out the rest, and it does not connect to neighboring regions.

It shows which packets arrive, not their contents. To see every field, decode packets in your own code with `PacketDecoder` from `LibreMetaverse.Protocol`, with Trace-level logging enabled.

It logs in with the account details you give it and nothing else, so use an account you are willing to use for testing.

## Requirements

.NET 8 or 9 runtime.

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Field-by-field packet decoding as a library: [`LibreMetaverse.Protocol`](https://www.nuget.org/packages/LibreMetaverse.Protocol)
- License: BSD-3-Clause
