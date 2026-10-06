# zand-uofl

A project evaluating optimization strategies and configuration settings
for a real-time 2D physics engine that combines a cellular-automata (CA)
particle simulation with rigid-body (RB) dynamics.

Final project for the UofL MsC

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Git](https://git-scm.com/)
- Windows 11 (version 10.0.26200). This project was developed and tested only on Windows 11; other
  platforms have not been tested. The renderer is MonoGame DesktopGL.

## Getting the code

This repo uses a custom build of [ChipmunkSharp](https://github.com/JamesGould123/ChipmunkSharp)
(a C# port of the Chipmunk2D physics engine) that fixes a crash caused by a hash collision in Chipmunk's
arbiter cache. It is included in this project as a git submodule in `external/ChipmunkSharp`.
Therefore it is recommended to clone with `--recurse-submodules`:

```
git clone --recurse-submodules https://github.com/JamesGould123/zand-uofl.git
cd zand-uofl
```

If you already cloned without that flag, fetch the submodule with:

```
git submodule update --init
```

The build will fail with a "project file not found" error for `ChipmunkSharp.csproj` if the
`ChipmunkSharp` submodule is not installed.

## Building

```
dotnet build Zand.sln -c Release
```

The first build restores the NuGet packages (MonoGame, ImGui.NET, Box2DSharp and others), so it requires
an internet connection.

## Running

### Interactive

```
dotnet run --project Zand.App -c Release
```

This opens a menu where you can configure a run with scenarios and configuration options along the 26 axes.

### Command line

```
dotnet run --project Zand.App -c Release -- --config path/to/config.json
```

Runs the configuration described by the JSON file without going through the menu. Adding `--dry-run`
prints the number of combinations and an estimated run time instead of running anything:

```
dotnet run --project Zand.App -c Release -- --config path/to/config.json --dry-run
```

Depending on which console you use, the dry-run output may not appear on its own, because `Zand.App`
is built as a Windows GUI application. Either send it to the console or write it to a file by
adding one of the following to the end of the command:

| Where | Add to the end of the command |
| --- | --- |
| PowerShell | ` \| Out-Host` |
| cmd | ` \| more` |
| A text file (most common consoles) | ` > dry-run.txt` |

### Results

When a run completes, the results are written to a timestamped folder under
`Documents\zand-uofl\runs\`. It contains:

- `manifest.csv` - one row per run, with its configuration and summary metrics
- `frames\run_XXXXX.csv` - the per-frame timings for each run
- `config.json` - the selection that produced the run

## Tests

```
dotnet test Zand.Core.Tests
```

## Projects

- `Zand.Core` - the simulation engine (CA, rigid-body backends, coupling), with no rendering dependencies.
- `Zand.Rendering` - MonoGame code for drawing cells, rigid bodies and debug overlays.
- `Zand.App` - the executable: game loop, scenarios, configuration menu and metrics export.
- `Zand.Core.Tests` - xUnit tests for `Zand.Core`.

## Dependencies

Package versions are pinned in the `.csproj` files so that a rebuild uses the same libraries the
results were measured with.

| Dependency | Version | Used for | Source |
| --- | --- | --- | --- |
| .NET | 9 (`net9.0`) | Runtime and SDK | https://dotnet.microsoft.com/ |
| Box2DSharp | 0.6.0 (NuGet) | Rigid-body backend (C# port of Box2D by Erin Catto) | https://github.com/Zonciu/Box2DSharp |
| ChipmunkSharp | fork at commit `d19b248` (submodule) | Rigid-body backend (C# port of Chipmunk2D by Scott Lembcke) | https://github.com/JamesGould123/ChipmunkSharp |
| MonoGame.Framework.DesktopGL | 3.8.5.1 (NuGet) | Game loop and rendering | https://monogame.net/ |
| MonoGame.Content.Builder.Task | 3.8.5.1 (NuGet) | Content pipeline build task | https://monogame.net/ |
| ImGui.NET | 1.91.6.1 (NuGet) | Configuration menu (wrapper for Dear ImGui by Omar Cornut) | https://github.com/ImGuiNET/ImGui.NET |

The ChipmunkSharp fork fixes a crash caused by a hash collision in the arbiter cache; the fix is
commit `d19b248` on the `v7.0.0.Leaf` branch. The original ChipmunkSharp port is by Jose Medrano.

## Licenses

See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
