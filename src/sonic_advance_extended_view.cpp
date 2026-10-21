#include "sonic_advance_extended_view.h"

#include <array>
#include <cstddef>
#include <cstdint>
#include <cstdio>
#include <limits>

#include "gba_bus.h"
#include "gba_ppu.h"
#include "runtime.h"
#include "runtime_bus_bridge.h"

namespace sonic_advance {
namespace {

// These addresses and field offsets are from the Sonic Advance 1 USA layout
// (SA1 IWRAM linker map): gStageBackgroundsRam is at 0x030058D0 and each
// Background is 0x40 bytes. The game keeps the complete stage metatile map in
// memory but normally uploads only the visible tile window into BG VRAM.
constexpr std::uint32_t kStageBackgroundsAddress = 0x030058D0u;
constexpr std::uint32_t kBackgroundSize = 0x40u;
constexpr std::uint32_t kLayoutOffset = 0x10u;
constexpr std::uint32_t kXTilesOffset = 0x14u;
constexpr std::uint32_t kYTilesOffset = 0x16u;
constexpr std::uint32_t kTilemapBaseXOffset = 0x1Eu;
constexpr std::uint32_t kTilemapBaseYOffset = 0x20u;
constexpr std::uint32_t kFlagsOffset = 0x2Eu;
constexpr std::uint32_t kScrollXOffset = 0x30u;
constexpr std::uint32_t kScrollYOffset = 0x32u;
constexpr std::uint32_t kMetatileMapOffset = 0x38u;
constexpr std::uint32_t kMapWidthOffset = 0x3Cu;
constexpr std::uint32_t kMapHeightOffset = 0x3Eu;
constexpr std::uint16_t kBackgroundIdMask = 0x0003u;
constexpr std::uint16_t kLevelMapFlag = 0x0040u;
constexpr std::uint16_t kFlipYFlag = 0x0080u;
constexpr std::uint16_t kFlipXFlag = 0x0100u;
constexpr std::uint16_t kTileFlipX = 0x0400u;
constexpr std::uint16_t kTileFlipY = 0x0800u;
constexpr std::uint32_t kEwramBase = 0x02000000u;
constexpr std::uint32_t kEwramEnd = 0x02040000u;
constexpr std::uint32_t kIwramBase = 0x03000000u;
constexpr std::uint32_t kIwramEnd = 0x03008000u;
constexpr std::uint32_t kRomBase = 0x08000000u;
constexpr std::uint32_t kRomEnd = 0x0A000000u;

struct BackgroundSource {
    bool valid = false;
    bool level_map = false;
    std::uint8_t bg_id = 0;
    std::uint16_t flags = 0;
    std::uint32_t layout = 0;
    std::uint32_t metatile_map = 0;
    std::uint16_t x_tiles = 0;
    std::uint16_t y_tiles = 0;
    std::uint16_t tilemap_base_x = 0;
    std::uint16_t tilemap_base_y = 0;
    std::int16_t scroll_x = 0;
    std::int16_t scroll_y = 0;
    std::uint16_t map_width = 0;
    std::uint16_t map_height = 0;
};

struct TileCacheEntry {
    bool valid = false;
    int tile_x = 0;
    int tile_y = 0;
    int action = 0;
    std::uint16_t value = 0;
};

std::array<BackgroundSource, 4> g_sources{};
std::array<TileCacheEntry, 4> g_last_tile{};
bool g_stage_map_active = false;

// Convert a guest address to a host pointer only when the entire requested
// span is inside a RAM or ROM range owned by the active GBA bus. This adapter
// is presentation-only and never writes guest memory.
const std::uint8_t* guest_bytes(const gba::GbaBus* bus,
                                std::uint32_t address,
                                std::size_t size) {
    if (!bus || size == 0) return nullptr;

    if (address >= kEwramBase && address < kEwramEnd &&
        size <= static_cast<std::size_t>(kEwramEnd - address)) {
        return bus->ewram_ptr() + (address - kEwramBase);
    }
    if (address >= kIwramBase && address < kIwramEnd &&
        size <= static_cast<std::size_t>(kIwramEnd - address)) {
        return bus->iwram_ptr() + (address - kIwramBase);
    }
    if (address >= kRomBase && address < kRomEnd) {
        const std::size_t offset = static_cast<std::size_t>(address - kRomBase);
        const std::size_t rom_size = bus->rom_size();
        const std::uint8_t* rom = bus->rom_ptr();
        if (rom && offset <= rom_size && size <= rom_size - offset)
            return rom + offset;
    }
    return nullptr;
}

bool read_u16(const gba::GbaBus* bus, std::uint32_t address,
              std::uint16_t* value) {
    const auto* p = guest_bytes(bus, address, 2);
    if (!p || !value) return false;
    *value = static_cast<std::uint16_t>(p[0]) |
             static_cast<std::uint16_t>(static_cast<std::uint16_t>(p[1]) << 8);
    return true;
}

bool read_u32(const gba::GbaBus* bus, std::uint32_t address,
              std::uint32_t* value) {
    const auto* p = guest_bytes(bus, address, 4);
    if (!p || !value) return false;
    *value = static_cast<std::uint32_t>(p[0]) |
             (static_cast<std::uint32_t>(p[1]) << 8) |
             (static_cast<std::uint32_t>(p[2]) << 16) |
             (static_cast<std::uint32_t>(p[3]) << 24);
    return true;
}

int floor_div(int value, int divisor) {
    if (value >= 0) return value / divisor;
    return -((-value + divisor - 1) / divisor);
}

int positive_mod(int value, int divisor) {
    const int result = value % divisor;
    return result < 0 ? result + divisor : result;
}

bool plausible_pointer_span(const gba::GbaBus* bus,
                            std::uint32_t address,
                            std::size_t size) {
    return guest_bytes(bus, address, size) != nullptr;
}

bool read_background(const gba::GbaBus* bus, int index,
                     BackgroundSource* out) {
    if (!bus || !out || index < 0 || index >= 4) return false;

    const std::uint32_t base =
        kStageBackgroundsAddress + static_cast<std::uint32_t>(index) * kBackgroundSize;
    BackgroundSource src;
    std::uint16_t raw_scroll_x = 0, raw_scroll_y = 0;

    if (!read_u32(bus, base + kLayoutOffset, &src.layout) ||
        !read_u16(bus, base + kXTilesOffset, &src.x_tiles) ||
        !read_u16(bus, base + kYTilesOffset, &src.y_tiles) ||
        !read_u16(bus, base + kTilemapBaseXOffset, &src.tilemap_base_x) ||
        !read_u16(bus, base + kTilemapBaseYOffset, &src.tilemap_base_y) ||
        !read_u16(bus, base + kFlagsOffset, &src.flags) ||
        !read_u16(bus, base + kScrollXOffset, &raw_scroll_x) ||
        !read_u16(bus, base + kScrollYOffset, &raw_scroll_y)) {
        return false;
    }

    src.scroll_x = static_cast<std::int16_t>(raw_scroll_x);
    src.scroll_y = static_cast<std::int16_t>(raw_scroll_y);
    src.bg_id = static_cast<std::uint8_t>(src.flags & kBackgroundIdMask);
    src.level_map = (src.flags & kLevelMapFlag) != 0;

    if (src.x_tiles == 0 || src.y_tiles == 0 ||
        src.x_tiles > 64 || src.y_tiles > 64 || src.layout == 0)
        return false;

    if (src.level_map) {
        std::uint64_t map_address = 0;
        std::uint16_t map_width = 0, map_height = 0;
        if (!read_u32(bus, base + kMetatileMapOffset, &src.metatile_map) ||
            !read_u16(bus, base + kMapWidthOffset, &map_width) ||
            !read_u16(bus, base + kMapHeightOffset, &map_height)) {
            return false;
        }
        src.map_width = map_width;
        src.map_height = map_height;
        if (src.metatile_map == 0 || src.map_width == 0 || src.map_height == 0 ||
            src.map_width > 4096 || src.map_height > 4096)
            return false;

        map_address = static_cast<std::uint64_t>(src.map_width) *
                      static_cast<std::uint64_t>(src.map_height) * 2u;
        if (map_address > std::numeric_limits<std::size_t>::max() ||
            !plausible_pointer_span(bus, src.metatile_map,
                                    static_cast<std::size_t>(map_address))) {
            return false;
        }
    } else {
        const std::uint64_t direct_bytes =
            static_cast<std::uint64_t>(src.x_tiles) * src.y_tiles * 2u;
        if (direct_bytes > std::numeric_limits<std::size_t>::max() ||
            !plausible_pointer_span(bus, src.layout,
                                    static_cast<std::size_t>(direct_bytes))) {
            return false;
        }
    }

    // Require at least the first source tile to be readable. Each tile read is
    // bounded again inside the provider, since the game can replace map pointers
    // during stage transitions.
    if (!plausible_pointer_span(bus, src.layout, 2)) return false;
    src.valid = true;
    *out = src;
    return true;
}

int supply_extended_tile(int bg_id, int screen_x, int screen_y,
                         std::uint16_t* out_entry) {
    if (!out_entry || bg_id < 0 || bg_id > 3 || !g_stage_map_active)
        return 0;
    auto* bus = gbarecomp::active_bus();
    if (!bus) return 0;

    const auto& source = g_sources[static_cast<std::size_t>(bg_id)];
    if (!source.valid) return 0;

    // The PPU calls this only outside the original 240x160 viewport. GBA HOFS
    // offsets remain in the game-owned scroll registers; adding the absolute
    // background scroll reconstructs the coordinate in the authored map.
    const int pixel_x = static_cast<int>(source.scroll_x) + screen_x;
    const int pixel_y = static_cast<int>(source.scroll_y) + screen_y;
    const int tile_x = floor_div(pixel_x, 8);
    const int tile_y = floor_div(pixel_y, 8);

    auto& cache = g_last_tile[static_cast<std::size_t>(bg_id)];
    if (cache.valid && cache.tile_x == tile_x && cache.tile_y == tile_y) {
        *out_entry = cache.value;
        return cache.action;
    }

    int action = 0;
    std::uint16_t entry = 0;

    if (source.level_map) {
        // A Sonic Advance stage map is a grid of metatiles. Each metatile points
        // into a 12x12-tile pattern in Background::layout. The entire map is
        // already resident/readable; only the 32x32 hardware tilemap is streamed.
        const int metatile_x = floor_div(tile_x, source.x_tiles);
        const int metatile_y = floor_div(tile_y, source.y_tiles);
        if (metatile_x < 0 || metatile_y < 0 ||
            metatile_x >= source.map_width || metatile_y >= source.map_height) {
            // Out of the authored map: leave this layer transparent instead
            // of wrapping the hardware's 32x32 ring or sampling tile zero.
            action = 0;
        } else {
            const std::uint64_t map_index =
                static_cast<std::uint64_t>(metatile_y) * source.map_width + metatile_x;
            const std::uint64_t map_address =
                static_cast<std::uint64_t>(source.metatile_map) + map_index * 2u;
            std::uint16_t metatile = 0;
            if (map_address <= std::numeric_limits<std::uint32_t>::max() &&
                read_u16(bus, static_cast<std::uint32_t>(map_address), &metatile)) {
                const int in_x = positive_mod(tile_x, source.x_tiles);
                const int in_y = positive_mod(tile_y, source.y_tiles);
                const std::uint64_t tiles_per_metatile =
                    static_cast<std::uint64_t>(source.x_tiles) * source.y_tiles;
                const std::uint64_t layout_index =
                    static_cast<std::uint64_t>(metatile) * tiles_per_metatile +
                    static_cast<std::uint64_t>(in_y) * source.x_tiles + in_x;
                const std::uint64_t layout_address =
                    static_cast<std::uint64_t>(source.layout) + layout_index * 2u;
                if (layout_address <= std::numeric_limits<std::uint32_t>::max() &&
                    read_u16(bus, static_cast<std::uint32_t>(layout_address), &entry)) {
                    action = 1;
                }
            }
        }
    } else {
        // Parallax/sky backgrounds use a direct tilemap rather than a stage
        // metatile grid. Preserve their own scrolling origin and wrap policy.
        int source_x = tile_x + source.tilemap_base_x;
        int source_y = tile_y + source.tilemap_base_y;
        source_x = positive_mod(source_x, source.x_tiles);
        source_y = positive_mod(source_y, source.y_tiles);
        if (source.flags & kFlipXFlag) {
            source_x = source.x_tiles - 1 - source_x;
        }
        if (source.flags & kFlipYFlag) {
            source_y = source.y_tiles - 1 - source_y;
        }
        const std::uint64_t layout_index =
            static_cast<std::uint64_t>(source_y) * source.x_tiles + source_x;
        const std::uint64_t layout_address =
            static_cast<std::uint64_t>(source.layout) + layout_index * 2u;
        if (layout_address <= std::numeric_limits<std::uint32_t>::max() &&
            read_u16(bus, static_cast<std::uint32_t>(layout_address), &entry)) {
            if (source.flags & kFlipXFlag) entry ^= kTileFlipX;
            if (source.flags & kFlipYFlag) entry ^= kTileFlipY;
            action = 1;
        }
    }

    cache.valid = true;
    cache.tile_x = tile_x;
    cache.tile_y = tile_y;
    cache.action = action;
    cache.value = entry;
    *out_entry = entry;
    return action;
}

}  // namespace

void install_extended_view(std::uint32_t, std::uint32_t) {
    g_sources = {};
    g_last_tile = {};
    g_stage_map_active = false;
    gba::g_ws_tilemap_provider = &supply_extended_tile;
    // The provider returns an authored tile or transparent for every margin
    // sample. Never fall back to the hardware's wrapping 32x32 tilemap ring.
    gba::g_ws_authored_margin_layers = 1;
    gba::g_ws_pillarbox = 1;
    gba::g_ws_pillarbox_left = 0;
    gba::g_ws_pillarbox_right = 0;
    std::fprintf(stderr,
                 "[sonic-advance:view] authored stage-map widescreen provider installed\n");
}

void update_extended_view(const gbarecomp::ExtendedViewFrameInfo* frame) {
    auto* bus = gbarecomp::active_bus();
    g_sources = {};
    g_last_tile = {};
    g_stage_map_active = false;

    if (!bus) {
        gba::g_ws_pillarbox = 1;
        return;
    }

    for (int index = 0; index < 4; ++index) {
        BackgroundSource src;
        if (!read_background(bus, index, &src)) continue;
        const std::size_t bg_id = src.bg_id;
        // If two game-owned descriptors ever target the same hardware BG,
        // prefer the actual level map over an old or non-map background.
        if (!g_sources[bg_id].valid ||
            (!g_sources[bg_id].level_map && src.level_map)) {
            g_sources[bg_id] = src;
        }
    }

    std::uint16_t dispcnt = 0xFFFFu;
    if (frame && frame->io && frame->io_size >= 2) {
        dispcnt = static_cast<std::uint16_t>(frame->io[0]) |
                  static_cast<std::uint16_t>(
                      static_cast<std::uint16_t>(frame->io[1]) << 8);
    }

    for (std::size_t bg = 0; bg < g_sources.size(); ++bg) {
        const auto& src = g_sources[bg];
        const bool bg_enabled = (dispcnt & (1u << (8u + bg))) != 0;
        if (src.valid && src.level_map && bg_enabled) {
            g_stage_map_active = true;
            break;
        }
    }

    // Menus/boss transitions that no longer expose a valid stage map retain the
    // authentic 240x160 image in the middle with black margins. When a live
    // stage map is present, the provider fills each side from the real map and
    // background data, not by stretching or repeating the framebuffer.
    gba::g_ws_pillarbox = g_stage_map_active ? 0 : 1;
}

}  // namespace sonic_advance
