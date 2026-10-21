#include <cstdint>
#include <cstdio>
#include <cstring>
#include <filesystem>
#include <string>
#include <system_error>
#include <vector>

#include "runtime.h"
#include "sonic_advance_extended_view.h"

#if defined(GBAGAME_RECOMP_UI)
#include "game_launcher_boot.h"
#endif

namespace {
constexpr const char* kGameName = "Sonic Advance";
constexpr const char* kRomSha1 = "d842afa7dd1e84de08addb94a51506f1bcafd551";
constexpr std::uint32_t kRomCrc32 = 0x63F70FD8u;

void print_usage() {
    std::printf(
        "SonicAdvanceRecomp [--rom <path>] [--bios <path>] [game.toml]\n"
        "Requires Sonic Advance USA (ASOE, revision 0) and a compatible GBA BIOS.\n"
        "Run without an explicit ROM path to open the integrated launcher.\n");
}
}  // namespace

int main(int argc, char** argv) {
    // Treat the executable folder as the working directory. The Builder writes
    // game.toml next to SonicAdvanceRecomp.exe and stores local absolute paths
    // there, so launching from Explorer or a shortcut finds the same config.
    std::error_code fsError;
    std::filesystem::path executablePath = argc > 0 && argv[0]
        ? std::filesystem::path(argv[0])
        : std::filesystem::current_path();
    executablePath = std::filesystem::absolute(executablePath, fsError);
    if (fsError) {
        fsError.clear();
        executablePath = std::filesystem::current_path() / executablePath;
    }
    const auto executableDirectory = executablePath.has_parent_path()
        ? executablePath.parent_path()
        : std::filesystem::current_path();
    std::filesystem::current_path(executableDirectory, fsError);

    for (int i = 1; i < argc; ++i) {
        if (std::strcmp(argv[i], "--help") == 0 ||
            std::strcmp(argv[i], "-h") == 0) {
            print_usage();
            return 0;
        }
    }

    const std::string gameConfigPath = (executableDirectory / "game.toml").string();
    gbarecomp::RunOptions options;
    options.builtin_game_name = kGameName;
    options.builtin_rom_sha1 = kRomSha1;
    options.builtin_rom_crc32 = kRomCrc32;
    options.launcher_region = "USA";
    options.launcher_game_config = gameConfigPath.c_str();
    options.launcher_save_path = "saves/sonic_advance_usa.sav";
    // Host presentation: allow window resizing and the framework fullscreen hotkey
    // (Alt+Enter by default). Adaptive widening remains opt-in in Display settings.
    // The game-owned adapter reads the full live stage-map source for margins;
    // stage-specific actor culling, HUD anchoring and transition coverage remain experimental.
    options.freely_resizable_window = true;
    options.resize_driven_view = true;
    options.max_resize_view_width = 480;
    options.extended_view_init = &sonic_advance::install_extended_view;
    options.extended_view_frame = &sonic_advance::update_extended_view;
    options.launcher_expose_adaptive_view = true;

#if defined(GBAGAME_RECOMP_UI)
    // Like MinishCapRecomp, the published game EXE contains the launcher UI and
    // the statically generated guest code. Recompilation from the user's ROM
    // is handled by the separate Builder EXE before this runner is produced.
    std::vector<std::string> args(argv, argv + argc);
    if (game_launcher_preboot(args, options)) return 0;

    std::vector<char*> forwarded_args;
    forwarded_args.reserve(args.size());
    for (auto& arg : args) forwarded_args.push_back(arg.data());
    return gbarecomp::run_game(
        static_cast<int>(forwarded_args.size()), forwarded_args.data(), options);
#else
    return gbarecomp::run_game(argc, argv, options);
#endif
}
