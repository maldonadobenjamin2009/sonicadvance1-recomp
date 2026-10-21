#pragma once

#include <string>
#include <vector>

#include "runtime.h"

// Runs the integrated recomp-ui pre-boot screen. Returns non-zero when the
// player closes the launcher; otherwise appends the launcher's settings to
// args so the existing runtime can process them normally.
int game_launcher_preboot(std::vector<std::string>& args,
                          const gbarecomp::RunOptions& options);
