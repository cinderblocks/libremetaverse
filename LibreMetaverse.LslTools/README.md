```
▗▖   ▄ ▗▖    ▄▄▄ ▗▞▀▚▖▗▖  ▗▖▗▞▀▚▖   ■  ▗▞▀▜▌▄   ▄ ▗▞▀▚▖ ▄▄▄ ▄▄▄ ▗▞▀▚▖
▐▌   ▄ ▐▌   █    ▐▛▀▀▘▐▛▚▞▜▌▐▛▀▀▘▗▄▟▙▄▖▝▚▄▟▌█   █ ▐▛▀▀▘█   ▀▄▄  ▐▛▀▀▘
▐▌   █ ▐▛▀▚▖█    ▝▚▄▄▖▐▌  ▐▌▝▚▄▄▖  ▐▌        ▀▄▀  ▝▚▄▄▖█   ▄▄▄▀ ▝▚▄▄▖
▐▙▄▄▖█ ▐▙▄▞▘          ▐▌  ▐▌       ▐▌                                
                                   ▐▌                                
```

# LibreMetaverse.LslTools

A lexer and parser toolkit for building tools that read the Linden Scripting Language. It is regular-expression driven tokenizing plus a table-driven shift/reduce parser, with the token and production classes that go with them, in the style of the CSTools lexer/parser generator.

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

  on their own: PrimMesher · Imaging.Skia · RLV · [LslTools]
```

This package stands apart from the stack: it depends on no other LibreMetaverse package.

## What is in it

- **Lexing**: `Lexer`, `Regex`, `Nfa` and `Dfa`, which build a tokenizer from patterns, and the `TOKEN` classes it produces.
- **Parsing**: `Parser`, `ParseState`, `Production`, `Precedence` and the shift/reduce tables built from them.
- **Support**: `Builder`, `ErrorHandler`, `SourceLineInfo` and the `CSTools...Exception` types for reporting where a script went wrong.

## Use it when

You are building a tool that reads LSL source (a syntax checker, a formatter, a highlighter, a converter) and want to define the grammar yourself rather than hand-write a scanner. The package provides the machinery, not a finished LSL grammar. For the list of keywords, functions and constants a grid currently supports (the syntax data the viewer downloads), see `LslSyntax` in the `LibreMetaverse` package.

## Depends on

Nothing outside the .NET libraries. It targets .NET Standard 2.1, .NET Framework 4.8.1, and .NET 8, 9 and 10.

## Install

```
dotnet add package LibreMetaverse.LslTools
```

## More

- Source, issues and discussion: https://github.com/cinderblocks/libremetaverse
- Moving from 3.1: [the migration guide](https://github.com/cinderblocks/libremetaverse/blob/master/MIGRATION.md)
- License: BSD-3-Clause
