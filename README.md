# Sonic Advance 1 Recomp

Experimental native recompilation of Sonic Advance (GBA) using
[GBARecomp](https://github.com/mstan/gbarecomp).

## The Windows Builder EXE

The **SonicAdvanceRecompBuilder.exe** artifact produced by GitHub Actions is a
single-file Windows GUI. Open it, choose your own ROM, choose a compatible GBA
BIOS, select an output directory, and press **GENERAR EXE DEL JUEGO**. The
builder verifies the exact ROM and BIOS revision, generates C/C++ code locally,
builds it with GBARecomp + CMake/Ninja/GCC, and writes
`SonicAdvanceRecomp.exe` together with the runtime DLLs, launcher assets and
`game.toml` into your selected output folder.

**The builder itself does not need a ROM uploaded to GitHub Actions.** The ROM
and BIOS are selected in the downloaded Windows application and stay on your
computer; they are not transmitted to GitHub and are not included in the output.
The build kit is embedded in the builder EXE.

### Requirements

The builder needs an MSYS2 native toolchain installed locally: **MINGW64 or UCRT64**, including GCC/G++, CMake, Ninja and SDL2. It detects either complete environment and runs the tools for you; you do not have to type the CMake/build commands manually. The embedded GBARecomp CLI includes its required MinGW runtime DLLs. If tools are missing, the Builder lists what's missing and gives the matching pacman commands.

Install/update MSYS2 from [msys2.org](https://www.msys2.org/). Open the **MSYS2
MINGW64** terminal and run:

~~~bash
pacman -Suy
~~~

Restart the terminal if requested, repeat `pacman -Suy` until fully updated,
then install:

~~~bash
pacman -S --needed mingw-w64-x86_64-toolchain mingw-w64-x86_64-cmake mingw-w64-x86_64-ninja mingw-w64-x86_64-SDL2 git
~~~

Common paths are `C:\msys64\mingw64\bin` and `C:\msys64\ucrt64\bin`. Use a consistent package family inside one MSYS2 environment; don't mix MINGW64 package names into UCRT64.

### Download the Builder

Open the [Sonic Advance Recomp Builder EXE workflow](https://github.com/maldonadobenjamin2009/sonicadvance1-recomp/actions/workflows/launcher-ci.yml), select the latest successful run, and download the artifact named
`SonicAdvanceRecompBuilder-windows-x64`. Extract the artifact ZIP and run
`SonicAdvanceRecompBuilder.exe`.

Choose these items in the app:
1. Your **Sonic Advance USA (ASOE, revision 0)** ROM.
2. A compatible retail GBA BIOS image.
3. The output folder for the game executable.

The exact accepted ROM SHA-1 is
`d842afa7dd1e84de08addb94a51506f1bcafd551`; the GBA BIOS SHA-1 is
`300c20df6731a33952ded8c436f7f186d25d3492`. Other ROM revisions are rejected
until they have a separately verified configuration. The generated executable
is an experimental port and is not yet guaranteed to boot or be fully playable.

## Fullscreen and widescreen (experimental)

The runner enables a freely resizable SDL window. **Alt+Enter** toggles borderless fullscreen by default; the shared runtime settings let you rebind the hotkey. The image keeps its GBA aspect ratio instead of stretching to fill the display.

An opt-in **Adaptive view** mode is also exposed in the runtime's Display settings. It can widen the logical PPU presentation up to 480 pixels while preserving the original 160-pixel height, following the same shared `RunOptions` capability used by MinishCapRecomp and EmeraldRecomp.

The game-owned adapter in `src/sonic_advance_extended_view.cpp` reads the live SA1 `gStageBackgroundsRam` records and resolves margin tiles through the full `metatileMap` and `layout` sources. The PPU no longer has to reuse unrelated entries from its 32×32 hardware tilemap ring for those samples; pixels outside the authored stage map remain transparent, and margins are pillarboxed when no active stage map is available. The reference data layout comes from the open-source [Sonic Advance decompilation](https://github.com/SAT-R/sa2).

This is still experimental. The original 240×160 game simulation and central viewport remain unchanged; the expanded view reveals more of the existing stage map rather than changing physics. Sonic's entity spawn/cull guard is wider than the native viewport and accommodates the current 480-pixel ceiling, but individual actor draw checks, HUD edge anchoring, bosses, special stages, and every transition have not yet been qualified in-game. The original 240×160 mode remains the default.

## Relationship to MinishCapRecomp

[MinishCapRecomp](https://github.com/mstan/MinishCapRecomp) does not create a new
EXE in its launcher at runtime. Its static game code is generated during the
build, linked into the game EXE, and its pre-boot launcher asks the player for
the matching ROM/BIOS to run. This project follows that model for the game
runner and adds a separate **builder EXE** to automate the native generation
and compilation after the user selects their local files.

The integrated game executable includes the [recomp-ui](https://github.com/mstan/recomp-ui)
pre-boot launcher, based on the same pattern used by MinishCapRecomp.

## Build from source

Requirements: Git, .NET 8 SDK, CMake, Ninja, and MSYS2 MINGW64 GCC/G++ and SDL2.
Initialize the pinned GBARecomp and recomp-ui submodules:

~~~powershell
git submodule update --init --recursive
~~~

Place your own files locally (never commit them), then run:

~~~powershell
$env:Path = "C:\msys64\mingw64\bin;C:\msys64\usr\bin;$env:Path"
.\scripts\build-native.ps1 -Rom "C:\path\to\sonic_advance_usa.gba" -Bios "C:\path\to\gba_bios.bin"
~~~

Output: `build-native/SonicAdvanceRecomp.exe`. ROM/BIOS-generated C/C++ files,
ROMs, BIOS dumps, and saves must never be committed.

## Legal / distribution

Unofficial preservation and research project; not affiliated with SEGA or
Nintendo. No copyrighted ROM or BIOS data is distributed. The builder embeds
open-source source code and a prebuilt GBARecomp CLI only.
