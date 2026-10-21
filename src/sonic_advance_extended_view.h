#pragma once

#include <cstdint>

namespace gbarecomp {
struct ExtendedViewFrameInfo;
}

namespace sonic_advance {

// Installs the sidecar that reads Sonic Advance's live full-map descriptors
// and supplies genuine off-screen tile entries to GBARecomp's expanded PPU.
void install_extended_view(std::uint32_t extra_left,
                           std::uint32_t extra_right);

// Refreshes read-only stage-map descriptors and disables expanded margins on
// menus/transitions that no longer expose an active stage map.
void update_extended_view(const gbarecomp::ExtendedViewFrameInfo* frame);

}  // namespace sonic_advance
