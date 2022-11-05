```
 _ _ _            __  __ ___ _____ ___   _____ ___  ___ ___ 
| (_) |__ _ _ ___|  \/  | __|_   _/_\ \ / / __| _ \/ __| __|
| | | '_ \ '_/ -_) |\/| | _|  | |/ _ \ V /| _||   /\__ \ _| 
|_|_|_.__/_| \___|_|  |_|___| |_/_/ \_\_/ |___|_|_\|___/___|
```
# LibreMetaverse

LibreMetaverse is a fork of libOpenMetaverse which in turn was a fork of
libSecondLife, a library for developing Second Life-compatible virtual world
clients. LibreMetaverse returns the focus to up-to-date Second Life and OpenSim
compatibility with an eye to performance, multi-threading, and memory management.

The canonical source for LibreMetaverse can be found at:
https://github.com/cinderblocks/libremetaverse

## Simple installation procedure

### Linux/macOS

-  Make sure you have at least `dotnet` installed, with a valid SDK _and_ runtime of at least .NET Core 3.1 available!

-  This update includes a solution file to skip the GUI applications (which will run only under Windows anyway). Use `LibreMetaverse.ReleaseNoGUI.sln` instead

-  From the root, run `dotnet restore LibreMetaverse.ReleaseNoGUI.sln`. You should get some errors regarding missing Windows libraries; that's ok, you can ignore those, they're to be expected since Linux/macOS do _not_ include such libraries. Some test applications are Windows-only.  
If all goes well, you should now have all dependent packages properly installed.

-  From the root, run `dotnet msbuild LibreMetaverse.ReleaseNoGUI.sln`, and enjoy the superfast Roslyn compiler at work 😄 It should finish after a few minutes, depending on the speed of your machine.

-  Your binaries will be under `../bin/net5.0` or `../bin/net6.0` (there might be a few more directories under `../bin`), depending on what runtimes you have installed on your system. Make sure you `cd` to the correct directory depending on the runtime you have, and then search for all your binaries there: they should be normal-looking executable files (with the `x` attribute set) and having the name of the appropriate test application (e.g. `TestClient` for the interactive testing tool).

-  Unlike OpenSimulator, you don't need to launch the binaries with Mono, they're _directly_ executable; the `dotnet` chain already embeds the small runtime that allows .NET apps to run natively on whatever operating system you've got.

### Windows

The `dotnet` utility is cross-platform so compilation is no different than on Linux/macOS. You may, however, opt to use Visual Studio as you would any other .NET application.

### GUI support under Linux/macOS

LibreMetaverse.GUI is not available on Linux/macOS due to Microsoft's lack of support for GDI/WinForms on non-Windows platforms. 
They may work using Mono's implemention and can always be emulated using a product like Wine.

The GUI library is being phased out and eventually being replaced by a more cross-platform framework like Avalonia or MAUI.

## Note: end-of-life support for .NET 5.0

Microsoft is [dropping support for .NET 5.0](https://devblogs.microsoft.com/dotnet/dotnet-5-end-of-support-update/) as of May 2022, so you should consider using .NET 6.0 instead (or, if you're wild, you can test the prerelease of .NET 7.0). The code runs flawlessly on .NET 6.0 as well.

[![LibreMetaverse NuGet-Release](https://img.shields.io/nuget/v/libremetaverse.svg?label=LibreMetaverse)](https://www.nuget.org/packages/LibreMetaverse/)  
[![NuGet Downloads](https://img.shields.io/nuget/dt/LibreMetaverse?label=NuGet%20downloads)](https://www.nuget.org/packages/LibreMetaverse/)  
[![Build status](https://ci.appveyor.com/api/projects/status/pga5w0qken2k2nnl?svg=true)](https://ci.appveyor.com/project/cinderblocks57647/libremetaverse-ksbcr)  
[![Test status](https://img.shields.io/appveyor/tests/cinderblocks57647/libremetaverse-ksbcr?compact_message&svg=true)](https://ci.appveyor.com/project/cinderblocks57647/libremetaverse-ksbcr)  
[![Codacy Badge](https://app.codacy.com/project/badge/Grade/1cb97cd799c64ba49e2721f2ddda56ab)](https://www.codacy.com/gh/cinderblocks/libremetaverse/dashboard?utm_source=github.com&amp;utm_medium=referral&amp;utm_content=cinderblocks/libremetaverse&amp;utm_campaign=Badge_Grade)  
[![.NET](https://github.com/cinderblocks/libremetaverse/actions/workflows/dotnet.yml/badge.svg)](https://github.com/cinderblocks/libremetaverse/actions/workflows/dotnet.yml)  
[![CodeQL](https://github.com/cinderblocks/libremetaverse/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/cinderblocks/libremetaverse/actions/workflows/codeql-analysis.yml)  
[![BSD Licensed](https://img.shields.io/github/license/cinderblocks/libremetaverse)](https://github.com/cinderblocks/libremetaverse/blob/master/LICENSE.txt)  
[![Commits per month](https://img.shields.io/github/commit-activity/m/cinderblocks/libremetaverse/master)](https://www.github.com/cinderblocks/libremetaverse/)  
[![ZEC](https://img.shields.io/keybase/zec/cinder)](https://keybase.io/cinder) [![BTC](https://img.shields.io/keybase/btc/cinder)](https://keybase.io/cinder)  
