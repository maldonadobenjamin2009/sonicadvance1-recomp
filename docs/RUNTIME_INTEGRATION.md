# GBARecomp runtime and Builder EXE

This project separates two stages, similar to
[MinishCapRecomp](https://github.com/mstan/MinishCapRecomp):

1. **Builder EXE** — takes the player's local Sonic Advance USA ROM and
   compatible GBA BIOS, then runs the static recompiler and native compiler on
   the player's Windows machine.
2. **Game EXE** — the resulting `SonicAdvanceRecomp.exe` contains the generated
   code for the supported Sonic Advance revision, the GBARecomp runtime and
   MinishCap-style pre-boot UI. The ROM/BIOS stay external and are selected by
   the user for runtime verification.

## Why this is local

GitHub's `workflow_dispatch` inputs are not a native binary-file upload field.
The public Action therefore builds a **builder application**, not a game from a
ROM it cannot receive. The user opens the Builder EXE and selects their local
ROM and BIOS; the generated native C/C++ is compiled locally. The ROM, BIOS,
and generated code are not uploaded or distributed.

## Build prerequisites

Windows, MSYS2 MINGW64, GCC/G++, CMake, Ninja and SDL2. The builder runs the
scripts automatically but the compiler and native libraries must be installed.
MINGW64 binaries are at `C:\msys64\mingw64\bin`; UCRT64 binaries are at
`C:\msys64\ucrt64\bin`. MSYS2's user utilities are at `C:\msys64\usr\bin`.

## ROM and BIOS identity

The only configured ROM profile currently is Sonic Advance USA (ASOE, revision 0):

- ROM SHA-1: `d842afa7dd1e84de08addb94a51506f1bcafd551`
- ROM MD5: `49ecc8ef1988e7ae81fbda1b4cf71eed`
- ROM CRC32: `63F70FD8`
- BIOS SHA-1: `300c20df6731a33952ded8c436f7f186d25d3492`

Other regions, revisions, patched or trimmed ROMs are rejected until configured
and reviewed separately.

## Running the Builder

Download the `SonicAdvanceRecompBuilder-windows-x64` artifact from the successful
[Sonic Advance Recomp Builder EXE workflow](https://github.com/maldonadobenjamin2009/sonicadvance1-recomp/actions/workflows/launcher-ci.yml).

The user selects a ROM, BIOS and output directory, then clicks **GENERAR EXE
DEL JUEGO**. The builder runs `scripts/build-native.ps1`, which recompiles BIOS
and game code, and then invokes CMake/Ninja to link the game. It copies the
resulting EXE, `game.toml`, SDL2 and MinGW runtime DLLs, and generated launcher
assets into the output folder. The ROM, BIOS and generated game C/C++ remain in
the user's local workspace and are not copied into that output folder.

## Relationship to MinishCapRecomp

MinishCap's published game EXE is already compiled from ROM-derived native code;
its pre-boot launcher selects/validates the matching user ROM/BIOS. The ROM does
not get translated into a new EXE by that launcher. Here, the Builder EXE is an
extra authoring tool that automates the compile process before producing the
MinishCap-style game EXE.

## Validation status

This remains an integration scaffold, not a validated playable Sonic Advance
port. CI can build the builder and check GBARecomp/launcher sources, but the
actual game-code build must use the user's exact ROM/BIOS locally and remains
subject to compilation and gameplay testing. Do not publish ROM/BIOS files,
saves, or ROM-derived generated C/C++.
