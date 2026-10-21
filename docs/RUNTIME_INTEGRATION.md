# GBARecomp runtime and integrated launcher

The native runner uses the shared GBARecomp runtime and the same general
pre-boot launcher pattern as
[MinishCapRecomp](https://github.com/mstan/MinishCapRecomp). The
`recomp-ui` and `gbarecomp` submodules are pinned in the repository.

## What "one EXE like Minish Cap" means

`SonicAdvanceRecomp.exe` contains both the native game runner and the
pre-boot launcher. At startup the launcher lets the player select their own
ROM and BIOS, verifies the configured game identity, and passes ordinary
`--rom`/`--bios` arguments to the runtime. It remembers player settings.

The static recompiler itself runs at build time: `gba_recompile` reads the
verified ROM/BIOS and emits game-specific native C/C++ files, which are then
compiled and linked into the executable. The launcher is not a general-purpose
tool for turning any arbitrary GBA ROM into a new native executable at runtime.
That is the same key distinction in MinishCapRecomp's design.

## Supported ROM configuration

The starting profile targets **Sonic Advance (USA, ASOE, revision 0)**:

- SHA-1: `d842afa7dd1e84de08addb94a51506f1bcafd551`
- MD5: `49ecc8ef1988e7ae81fbda1b4cf71eed`
- CRC32: `63F70FD8`
- ROM size: 8 MiB
- Save device: 64 KiB Flash

The BIOS is checked against SHA-1
`300c20df6731a33952ded8c436f7f186d25d3492`. Build scripts reject an unknown
ROM revision or a mismatched BIOS.

## Local native build

Install Git, CMake, Ninja, and MSYS2 MINGW64 GCC/G++ plus SDL2. Initialize the
submodules:

~~~powershell
git submodule update --init --recursive
~~~

Place your own files locally, without committing them:

- `roms/sonic_advance_usa.gba`
- `gbarecomp/bios/gba_bios.bin`

Then build from PowerShell:

~~~powershell
$env:Path = "C:\msys64\mingw64\bin;C:\msys64\usr\bin;$env:Path"
.\scripts\build-native.ps1
~~~

The output is `build-native/SonicAdvanceRecomp.exe`. The launcher UI is linked
into the native executable; it is no longer necessary to select a separate
runner executable in the legacy WinForms helper.

## GitHub Actions workflow on a local runner

A GitHub-hosted runner does not have your ROM or BIOS, and this public project
does not contain generated ROM-derived source. Therefore the workflow that
builds the final game runs only on a Windows x64 self-hosted runner and is
triggered manually. Enter absolute local paths for the ROM and BIOS and an
output folder. It creates a ZIP containing the combined native EXE, launcher
assets and required DLLs on your PC. It intentionally does not publish this
ROM-derived package as a public Actions artifact.

The regular hosted CI only compile-checks the launcher library and the
GBARecomp toolchain; it does not claim that it has built a playable Sonic
Advance executable.

## Validation status and next tasks

This is still an integration scaffold, not yet a validated playable Sonic
Advance port. A successful compile proves that the toolchain and UI link
together, but not that Sonic boots or has correct controls, sound, saves,
interrupts, or copied-code behavior.

After the first native build, validate BIOS handoff, title screen, new game,
several zones, boss/bonus routes, sound, Flash save/reload and shutdown.
Capture unresolved indirect targets and code copied to RAM/stack; add
game-specific metadata only after checking it against the exact ROM. Do not
commit generated/, BIOS data, ROMs, saves, or runtime caches.
