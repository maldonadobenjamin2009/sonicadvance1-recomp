# GBARecomp runtime integration

This repository now includes the native runner integration layer used by the
public mstan GBARecomp game projects. The gbarecomp Git submodule is pinned to
the same revision used by the current WarioWare Twisted, Super Mario Advance 4,
and Mega Man Zero integrations; do not update it without rebuilding and
testing the game target.

## What the reference projects teach us

- [WarioWareTwistedRecomp](https://github.com/mstan/WarioWareTwistedRecomp):
  CMake links generated guest code with the runtime, debug, GBA hardware, and
  ARMv4T libraries; its config also documents code copied into RAM.
- [SuperMarioAdvance2Recomp](https://github.com/mstan/SuperMarioAdvance2Recomp)
  and [SuperMarioAdvance4Recomp](https://github.com/mstan/SuperMarioAdvance4Recomp):
  game-owned main entry point, RunOptions, game config, and optional pre-boot
  UI are separate from the shared runtime.
- [ShrekGBAVideoRecomp](https://github.com/mstan/ShrekGBAVideoRecomp) and
  [MegaManZeroRecomp](https://github.com/mstan/MegaManZeroRecomp): real ports
  are validated against one ROM revision; functions copied to RAM and indirect
  entries must be reviewed when execution traces expose them.
- [EmeraldRecomp](https://github.com/mstan/EmeraldRecomp),
  [FireRedLeafGreenRecomp](https://github.com/mstan/FireRedLeafGreenRecomp), and
  [RubySapphireRecomp](https://github.com/mstan/RubySapphireRecomp): separate
  runtime config and generated code from the game ROM and BIOS. The runtime
  can interpret or heal uncovered paths, but that is not proof of full static
  coverage.
- [Gen1Recomp](https://github.com/bryanthaboi/gen1recomp) is a LÖVE2D recreation,
  not a static ARM recompiler. Its ROM import, identity checks, separate local
  profiles, and controller configuration are UX references only; its engine
  cannot be linked as the GBA runtime.

These projects are architectural references, not sources of Sonic-specific
symbols or RAM-copy annotations. Sonic's config must be extended only from
verified ROM metadata and observed/tested execution.

## Supported ROM configuration

The starting profile targets **Sonic Advance (USA, ASOE, revision 0)**:

- SHA-1: d842afa7dd1e84de08addb94a51506f1bcafd551
- MD5: 49ecc8ef1988e7ae81fbda1b4cf71eed
- CRC32: 63F70FD8
- ROM size: 8 MiB
- Save device: 64 KiB Flash

The identity values correspond to the No-Intro USA dump as catalogued by
[TASVideos](https://tasvideos.org/Games/297/Versions/View/347) and
[GameHacking.org](https://gamehacking.org/game/5997). The build scripts reject
another revision; add a separately reviewed config before supporting another
region or revision. The GBA BIOS is also hash-checked by GBARecomp.

## Local build (Windows)

Install Git, CMake, Ninja, and MSYS2's **MINGW64** toolchain/SDL2. Initialize
the pinned runtime:

~~~powershell
git submodule update --init --recursive
~~~

Place your own files locally (never commit them):

- ROM: roms/sonic_advance_usa.gba
- BIOS: gbarecomp/bios/gba_bios.bin

From PowerShell, make sure MSYS2's MinGW64 tools are first on PATH and run:

~~~powershell
$env:Path = "C:\msys64\mingw64\bin;$env:Path"
.\scripts\build-native.ps1
~~~

The script verifies the ROM and BIOS hashes, builds the pinned gba_recompile
tool if required, recompiles the BIOS and game source into ignored local
directories, and invokes CMake to link SonicAdvanceRecomp.exe against the
runtime. You can override the local paths:

~~~powershell
.\scripts\build-native.ps1 -Rom "D:\Games\sonic_advance_usa.gba" -Bios "D:\BIOS\gba_bios.bin"
~~~

The output runner is build-native/SonicAdvanceRecomp.exe. The separate WinForms
launcher is built by the existing Launcher CI workflow and can point to that
runner. The runner accepts --rom, --bios, and --fullscreen.

## Validation status and next tasks

This is an integration scaffold and initial config, **not yet a validated
playable Sonic Advance port**. A successful compile only shows that the runtime
and generated translation units link. It does not demonstrate that Sonic boots
or has correct controls, sound, saves, interrupts, or copied-code behavior.

After the first native build, validate at least: BIOS handoff, title screen,
new game, several zones, boss/bonus routes, sound, flash save/reload, and
shutdown. Capture unresolved indirect targets and RAM/stack code copies; add
game-specific extra_func or code_copy entries only after confirming them
against the exact ROM. Compare behavior with a reference GBA emulator. Do not
commit generated/, BIOS data, ROMs, saves, or runtime caches.
