# Sonic Advance 1 Recomp

An experimental native-recompilation project for Sonic Advance (GBA), using
the shared [GBARecomp](https://github.com/mstan/gbarecomp) runtime.

## Current state

- The native `SonicAdvanceRecomp.exe` target now integrates the same style of
  Dear ImGui pre-boot launcher used by [MinishCapRecomp](https://github.com/mstan/MinishCapRecomp):
  choose a ROM and BIOS, configure launch settings, then start the game from
  that same executable.
- GBARecomp's `gba_recompile` tool generates game-specific native C/C++ during
  the build. The launcher is not a compiler for arbitrary ROMs at runtime;
  the static game code must already have been generated and linked into the
  executable.
- **This is not yet a validated playable Sonic Advance port.** Static coverage,
  BIOS handoff, copied-to-RAM functions and gameplay still need local testing.
- ROMs, BIOS images, generated ROM-derived C/C++ code, saves, caches and build
  outputs are not stored in this public repository.

## Reference implementations

The integrated startup flow follows
[MinishCapRecomp](https://github.com/mstan/MinishCapRecomp) and the shared
[recomp-ui](https://github.com/mstan/recomp-ui) launcher. Runtime linkage and
game configuration also follow patterns in
[WarioWareTwistedRecomp](https://github.com/mstan/WarioWareTwistedRecomp),
[SuperMarioAdvance2Recomp](https://github.com/mstan/SuperMarioAdvance2Recomp),
[SuperMarioAdvance4Recomp](https://github.com/mstan/SuperMarioAdvance4Recomp),
[ShrekGBAVideoRecomp](https://github.com/mstan/ShrekGBAVideoRecomp),
[MegaManZeroRecomp](https://github.com/mstan/MegaManZeroRecomp),
[EmeraldRecomp](https://github.com/mstan/EmeraldRecomp),
[FireRedLeafGreenRecomp](https://github.com/mstan/FireRedLeafGreenRecomp), and
[RubySapphireRecomp](https://github.com/mstan/RubySapphireRecomp).

[Gen1Recomp](https://github.com/bryanthaboi/gen1recomp) is a separate LÖVE2D
recreation, not a static GBA recompiler or runtime dependency.

## Supported ROM profile

The initial configuration targets **Sonic Advance (USA, ASOE, revision 0)**:

- SHA-1: `d842afa7dd1e84de08addb94a51506f1bcafd551`
- MD5: `49ecc8ef1988e7ae81fbda1b4cf71eed`
- CRC32: `63F70FD8`

Other regions/revisions are rejected until their own configuration is reviewed.
The BIOS identity is checked by the build script and runtime.

## Build on Windows

Requirements: Git, CMake, Ninja, and the MSYS2 MINGW64 GCC/G++ toolchain plus
SDL2. Initialize both pinned submodules:

~~~powershell
git submodule update --init --recursive
~~~

Keep your own files local; **do not commit them**:

- `roms/sonic_advance_usa.gba`
- `gbarecomp/bios/gba_bios.bin`

From PowerShell, with `C:\msys64\mingw64\bin` on PATH:

~~~powershell
$env:Path = "C:\msys64\mingw64\bin;C:\msys64\usr\bin;$env:Path"
.\scripts\build-native.ps1
~~~

The build generates local native source and creates
`build-native/SonicAdvanceRecomp.exe`. Launching it opens the integrated
pre-boot launcher when no explicit `--rom` is passed. Select your own ROM and
BIOS, and the launcher/runtime verifies the supported identity.

See [docs/RUNTIME_INTEGRATION.md](docs/RUNTIME_INTEGRATION.md) for architecture
and validation notes.

## Build using GitHub Actions on your PC

The regular GitHub-hosted CI compiles the recompiler tool and checks the
launcher UI, but it intentionally cannot build the actual game executable
without your local ROM, BIOS and generated sources.

For a button-driven build from the GitHub **Actions** tab, configure a
self-hosted Windows x64 runner for this repository on your own PC:
Settings → Actions → Runners → New self-hosted runner. Install the prerequisites
listed above first. Then open
[Build Sonic Advance Windows EXE (local runner)](https://github.com/maldonadobenjamin2009/sonicadvance1-recomp/actions/workflows/build-native-self-hosted.yml),
choose **Run workflow**, and provide the absolute paths to your own ROM and
BIOS plus a local output folder. The workflow writes
`SonicAdvanceRecomp-windows-x64.zip` into that folder on your PC. It does not
upload the game build as a public Actions artifact or send your ROM/BIOS.

Only run self-hosted workflows you trust: they execute code on your computer.
This workflow is manual-only and does not run for pull requests.

## Launcher

The native game executable now owns its pre-boot launcher, like MinishCapRecomp;
a separate WinForms launcher is still present as a legacy helper, but it is not
required for the integrated executable. The launcher's selections/settings
persist locally next to the executable.

## Legal and distribution

This is an unofficial preservation/research project and is not affiliated with
or endorsed by SEGA or Nintendo. Sonic Advance and related marks belong to
their respective owners. No ROM, BIOS, save, or ROM-derived generated code is
distributed here.
