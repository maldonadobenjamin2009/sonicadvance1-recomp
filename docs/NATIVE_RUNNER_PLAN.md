# Native runner integration plan

## What the generated library means

The standalone GBARecomp CLI output named gbarecomp_game.lib contains generated
game code only. It does not provide a window, audio, input, save-chip emulation,
BIOS execution, or a runtime entry point. Do not link this library to an arbitrary
main.cpp and assume the game will boot.

## Reference architecture

Use these projects as architectural references. Do not copy game-specific
configuration or code blindly:

- https://github.com/mstan/gbarecomp — runtime and recompiler.
- https://github.com/mstan/MinishCapRecomp — host executable, game config,
  ROM/BIOS identity validation and pre-boot launcher.
- https://github.com/mstan/EmeraldRecomp
- https://github.com/mstan/FireRedLeafGreenRecomp
- https://github.com/mstan/RubySapphireRecomp — Gen 3 integration patterns.
- https://github.com/bryanthaboi/gen1recomp — useful launcher UX ideas such as
  local ROM import and separate profiles. This is a LÖVE2D recreation, not the
  GBARecomp runtime, and its engine is not a drop-in dependency.

## Required steps for Sonic Advance

1. Pin a GBARecomp runtime revision and add it as a Git submodule.
2. Determine the exact supported Sonic Advance ROM revision from the user's
   local dump. Record verified SHA-1 and CRC32 values using trusted local
   tooling. Never guess these values and never upload the ROM.
3. Add a game configuration for ROM/BIOS identity, save-chip type, runtime
   options, recompiler entry point, symbol seeds and function boundaries.
   Verify every field against the current runtime schema.
4. Produce and independently verify function/symbol metadata for Sonic Advance.
   Do not copy Pokémon or Zelda symbols.
5. Regenerate code with the runtime-compatible gba_recompile version and its
   config/symbol inputs. Standalone CLI output may not match the pinned runtime.
6. Implement a game-specific entry point that configures RunOptions, installs
   any required RAM dispatch hooks and calls the runtime runner.
7. Link generated source files directly with runtime libraries following the
   current upstream CMake target graph.
8. Test boot, title screen, gameplay, input, audio, saves and shutdown against a
   reference emulator before publishing a playable build.

## Launcher contract

The Windows launcher is a separate process. It selects the user's ROM and BIOS,
remembers paths locally, computes the ROM SHA-1 for diagnostics, and invokes a
native runner with --rom <path> --bios <path>. The runner must support these
arguments and independently verify ROM and BIOS identities. The launcher does
not claim the generated library is playable.

## Automation

- launcher-ci.yml builds and uploads the Windows launcher artifact.
- validate-project.yml checks the source scaffold and guards against ROM/BIOS
  assets being committed.
- Do not enable a native runner release workflow until the runtime submodule,
  game config, symbol metadata and runner source exist. A workflow that pretends
  to turn the current static library into a playable game would be misleading.
