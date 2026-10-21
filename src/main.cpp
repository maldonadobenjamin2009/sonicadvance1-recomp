#include <cstdint>
#include <cstdio>
#include <cstring>

#include "runtime.h"

namespace {
constexpr const char* kGameName = "Sonic Advance";
constexpr const char* kRomSha1 = "d842afa7dd1e84de08addb94a51506f1bcafd551";
constexpr std::uint32_t kRomCrc32 = 0x63F70FD8u;

void print_usage() {
    std::printf(
        "SonicAdvanceRecomp [--rom <path>] [--bios <path>] [game.toml]\n"
        "Requires the verified Sonic Advance (USA, ASOE) ROM and a GBA BIOS dump.\n");
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

    return gbarecomp::run_game(argc, argv, options);
}
