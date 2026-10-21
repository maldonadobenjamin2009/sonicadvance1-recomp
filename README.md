# Sonic Advance 1 Recomp

An experimental native-recompilation project for Sonic Advance (GBA), using
the shared [GBARecomp](https://github.com/mstan/gbarecomp) runtime.

## Current state

- The Windows WinForms launcher builds through GitHub Actions and lets you
  select your own ROM, BIOS and native runner.
- The runtime is pinned as the gbarecomp Git submodule. The repository now
  includes a game-specific CMake target, runtime entry point, ROM/BIOS identity
  config, and local generation/build scripts.
- **This is not yet a validated playable port.** Generated files in generated/
  must be rebuilt locally from the exact supported ROM using the
  runtime-compatible gba_recompile tool. Runtime paths, static coverage and
  any copied-to-RAM functions still need gameplay validation.
- ROMs, BIOS images, generated ROM-derived C/C++ code, saves, caches and build
  outputs are not stored in this public repository.

## Reference implementations

The runtime linkage and per-game config follow patterns in
[WarioWareTwistedRecomp](https://github.com/mstan/WarioWareTwistedRecomp),
[SuperMarioAdvance2Recomp](https://github.com/mstan/SuperMarioAdvance2Recomp),
[SuperMarioAdvance4Recomp](https://github.com/mstan/SuperMarioAdvance4Recomp),
[ShrekGBAVideoRecomp](https://github.com/mstan/ShrekGBAVideoRecomp),
[MegaManZeroRecomp](https://github.com/mstan/MegaManZeroRecomp),
[EmeraldRecomp](https://github.com/mstan/EmeraldRecomp),
[FireRedLeafGreenRecomp](https://github.com/mstan/FireRedLeafGreenRecomp), and
[RubySapphireRecomp](https://github.com/mstan/RubySapphireRecomp).

[Gen1Recomp](https://github.com/bryanthaboi/gen1recomp) is a separate
LÖVE2D recreation rather than a static recompiler. It is useful as a UX
reference for ROM import, identity verification and separate local profiles,
but it is not a runtime dependency.

## Supported ROM profile

The initial configuration targets **Sonic Advance (USA, ASOE, revision 0)**:

- SHA-1: d842afa7dd1e84de08addb94a51506f1bcafd551
- MD5: 49ecc8ef1988e7ae81fbda1b4cf71eed
- CRC32: 63F70FD8

Other regions/revisions are rejected until their own configuration is reviewed.

## Build on Windows

Requirements: Git, .NET 8 SDK (launcher), CMake, Ninja, and MSYS2 MINGW64
GCC/G++ plus SDL2.

Initialize the pinned runtime submodule:

~~~powershell
git submodule update --init --recursive
~~~

Place your own files locally; **do not commit them**:

- roms/sonic_advance_usa.gba
- gbarecomp/bios/gba_bios.bin

From PowerShell:

~~~powershell
$env:Path = "C:\msys64\mingw64\bin;$env:Path"
.\scripts\build-native.ps1
~~~

The script verifies file identities, recompiles the BIOS and game code, then
builds build-native/SonicAdvanceRecomp.exe. See
[docs/RUNTIME_INTEGRATION.md](docs/RUNTIME_INTEGRATION.md) for the detailed
process, troubleshooting, and validation checklist.

## Launcher

The launcher is published by the **Launcher CI** workflow:

1. Open the [Actions page](https://github.com/maldonadobenjamin2009/sonicadvance1-recomp/actions/workflows/launcher-ci.yml).
2. Open the latest successful run.
3. Download the SonicAdvanceLauncher-windows-x64 artifact.
4. Extract the archive and run SonicAdvanceLauncher.exe.
5. Select your own ROM, BIOS and the locally built SonicAdvanceRecomp.exe.

The launcher itself is buildable now. Do not describe the native runner as
playable until BIOS handoff, title screen, gameplay, controls, audio and saves
have been verified.

## Legal and distribution

This is an unofficial preservation/research project and is not affiliated with
or endorsed by SEGA or Nintendo. Sonic Advance and related marks belong to
their respective owners. No ROM, BIOS, save or ROM-derived generated code is
distributed here.
