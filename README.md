# Sonic Advance 1 Recomp — launcher bootstrap

This repository is the starting point for a Windows launcher and the native
GBARecomp integration for **Sonic Advance (GBA)**.

## Current state

- The Windows launcher project can be built by GitHub Actions.
- It lets the user select a legally obtained ROM and GBA BIOS, computes the
  ROM SHA-1 for diagnostics (identity enforcement awaits a verified game config),
  remembers local paths, and starts a companion
  `SonicAdvanceRecomp.exe` when that executable has been built.
- **The playable native runner is not in this repository yet.** A generated
  `gbarecomp_game.lib` alone is not a game executable. The runner must link the
  generated game code to the GBARecomp runtime and its game configuration.
- ROMs, BIOS images, generated game code, save files, and build outputs must
  not be committed to this public repository.

## References and approach

The intended native port layout follows the game integration used by
[mstan/MinishCapRecomp](https://github.com/mstan/MinishCapRecomp) and the
Gen 3 native recompiler projects
[mstan/EmeraldRecomp](https://github.com/mstan/EmeraldRecomp),
[mstan/FireRedLeafGreenRecomp](https://github.com/mstan/FireRedLeafGreenRecomp),
and [mstan/RubySapphireRecomp](https://github.com/mstan/RubySapphireRecomp):
a per-game runtime configuration, a host executable, runtime linkage, ROM/BIOS
identity checks, and a pre-boot launcher.

[bryanthaboi/gen1recomp](https://github.com/bryanthaboi/gen1recomp) is a
separate LÖVE2D-based native recreation, not a static recompiler. Its useful
product-level ideas for this project are ROM import/verification, multiple
local profiles, persistent settings, and keyboard/controller configuration;
its engine is not interchangeable with GBARecomp.

## Build the launcher locally (Windows)

Install the .NET 8 SDK, then run from the repository root:

```powershell
dotnet publish .\launcher\SonicAdvanceLauncher.csproj -c Release -r win-x64 --self-contained true
```

The output is placed at:

```text
launcher/bin/Release/net8.0-windows/win-x64/publish/SonicAdvanceLauncher.exe
```

The same build is run by the `Launcher CI` workflow. Download its
`SonicAdvanceLauncher-windows-x64` artifact from the Actions run.

## Running it

Place `SonicAdvanceLauncher.exe` next to `SonicAdvanceRecomp.exe`, or select
the native runner manually in the launcher. Select your own Sonic Advance ROM
and GBA BIOS. The ROM/BIOS are not included in this repository or workflow.

The launcher can start only a compatible native runner; it cannot convert a
ROM or turn `gbarecomp_game.lib` into a playable game on its own.

## Next native-integration milestone

1. Import the GBARecomp runtime and launcher UI as pinned dependencies.
2. Add verified Sonic Advance game configuration and symbol/function metadata.
3. Regenerate code using the runtime-compatible `gba_recompile` flow.
4. Link the generated sources with the runtime to produce
   `SonicAdvanceRecomp.exe`.
5. Add runtime smoke tests for startup, input, video, audio, saves, and clean
   shutdown before publishing a playable build.
