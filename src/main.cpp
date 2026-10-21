#include <cstdint>
#include <cstdio>
#include <cstring>
#include <string>
#include <vector>

#include "runtime.h"

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
        "Requires the verified Sonic Advance (USA, ASOE) ROM and a GBA BIOS dump.\n"
        "Run without an explicit ROM path to open the integrated launcher.\n");
}
}  // namespace

int main(int argc, char** argv) {
    for (int i = 1; i < argc; ++i) {
        if (std::strcmp(argv[i], "--help") == 0 ||
            std::strcmp(argv[i], "-h") == 0) {
            print_usage();
            return 0;
        }
    }

    gbarecomp::RunOptions options;
    options.builtin_game_name = kGameName;
    options.builtin_rom_sha1 = kRomSha1;
    options.builtin_rom_crc32 = kRomCrc32;
    options.launcher_region = "USA";
    options.launcher_game_config = "game.toml";
    options.launcher_save_path = "saves/sonic_advance_usa.sav";

#if defined(GBAGAME_RECOMP_UI)
    // Like MinishCapRecomp, the pre-boot UI only chooses and verifies the
    // player's own files/settings. The static game code was generated when
    // this executable was built; this is not an arbitrary-ROM compiler UI.
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
