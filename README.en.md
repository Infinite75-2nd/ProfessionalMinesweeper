# Professional Minesweeper

[简体中文](README.md)

[GitHub repository](https://github.com/Infinite75-2nd/ProfessionalMinesweeper)

A multi-geometry Minesweeper implementation built with **C#, .NET 10 and Avalonia UI 12**, for Windows and Android.

This project's code is **AI-assisted**, including parts of the implementation, refactoring, documentation and tests. Development and iteration are coordinated by the author.

## Origin

This project recreates the original **Professional Minesweeper (`profmine.exe`)**. Board layouts, menu ordering and preset research are based on earlier reverse-engineering work, including board data and adjacency read from the running original game. This is an independent implementation, not an official release by the original author. Original game names and third-party assets remain the property of their respective owners.

## Features

- 18 original boards plus 5 original designs: Crystal Shards (Voronoi of a golden-angle spiral), Vortex (concentric rings twisted ring by ring), Honeycomb Triangles (medial honeycomb with triangular gaps), L-trominoes (2x3 blocks split into two L-shaped cells) and Shard Faces (jittered lattice split into irregular triangles).
- Three difficulties, deferred mine placement, first-click protection, flags/question marks, chording, timer and game status.
- Windows: wheel to pan vertically, Shift + wheel horizontally, Ctrl + wheel to zoom, and left-button dragging.
- Android: drag to pan, pinch to zoom, hold covered cells to flag, hold revealed cells to preview neighbors, and double-tap numbers to chord. Double-tap the board after a win or loss to restart.
- Windows Native AOT publishing; Android Release builds use Mono AOT.

## Build

Requires the .NET 10 SDK. Windows Native AOT also requires the Visual Studio C++ toolchain and Windows SDK. Android requires the `android` workload, a compatible JDK and Android SDK.

```powershell
dotnet build -c Release
dotnet test
dotnet publish src/ProfessionalMinesweeper.Desktop/ProfessionalMinesweeper.Desktop.csproj -c Release -r win-x64 --self-contained true
dotnet build src/ProfessionalMinesweeper.Android/ProfessionalMinesweeper.Android.csproj -c Release
```

Windows static linking uses the `2ndLAB.ANGLE.Static` and `2ndLAB.SkiaSharp.Static` NuGet packages; see `NativeAOT.props`. Windows output is under Desktop's `bin/Release/net10.0/win-x64/publish/`; APKs are under Android's `bin/Release/net10.0-android/`.

## Layout

- `src/ProfessionalMinesweeper.Core`: game logic, polygon topology, board generators and presets.
- `src/ProfessionalMinesweeper.Desktop`: Windows UI, rendering and mouse input.
- `src/ProfessionalMinesweeper.Android`: Android entry point, touch viewport and mobile UI.
- `tests/ProfessionalMinesweeper.Tests`: game logic, coverage, connectivity and performance regression tests.
- `re/`: original game analysis notes. The original executable and extracted sprite sheets are excluded from the repository.

## Author and license

**Infinite75** · [Bilibili profile](https://space.bilibili.com/672619350)

Project code is licensed under **GPL-3.0-only**; see [LICENSE](LICENSE). Dependencies retain their own licenses. Development is ongoing: successful builds do not imply that every board and touch interaction has been verified on physical devices.
