cmake_minimum_required(VERSION 3.20)
project(SonicAdvanceRecomp C CXX)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(GBARECOMP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/gbarecomp"
    CACHE PATH "Path to the pinned GBARecomp runtime checkout")
set(GBARECOMP_RUNTIME_UI_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/recomp-ui"
    CACHE PATH "recomp-ui root used by the live GBA runtime adapter")
set(RECOMP_UI_ROOT "${GBARECOMP_RUNTIME_UI_ROOT}"
    CACHE PATH "Path to the shared recomp-ui checkout")

if(NOT EXISTS "${GBARECOMP_ROOT}/CMakeLists.txt")
    message(FATAL_ERROR
        "GBARecomp runtime is missing at ${GBARECOMP_ROOT}. "
        "Run: git submodule update --init --recursive")
endif()

add_subdirectory("${GBARECOMP_ROOT}" gbarecomp_build EXCLUDE_FROM_ALL)

file(GLOB GAME_GENERATED_SOURCES CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/generated/*.c"
    "${CMAKE_CURRENT_SOURCE_DIR}/generated/*.cpp")

option(SONIC_BUILD_GAME_EXECUTABLE
    "Build the Sonic Advance runner (requires locally generated game source)" ON)
option(GBAGAME_RECOMP_UI
    "Build the integrated recomp-ui pre-boot launcher" ON)

# Keep generated ROM-derived source local. A toolchain-only CI build can
# compile the launcher library without having the user's ROM or BIOS.
if(SONIC_BUILD_GAME_EXECUTABLE)
    if(NOT GAME_GENERATED_SOURCES)
        message(FATAL_ERROR
            "No runtime-compatible generated game source was found in generated/. "
            "Run scripts/regen.ps1 with your own verified Sonic Advance USA ROM "
            "and GBA BIOS first. The old standalone CLI output is not sufficient.")
    endif()

    add_executable(SonicAdvanceRecomp
        src/main.cpp
        ${GAME_GENERATED_SOURCES})

    set(GAME_RECOMPILED_SOURCES ${GAME_GENERATED_SOURCES})
    list(FILTER GAME_RECOMPILED_SOURCES INCLUDE REGEX
        "/recompiled_[0-9]+\\.(c|cpp)$")
    if(GAME_RECOMPILED_SOURCES)
        if(MSVC)
            set_source_files_properties(${GAME_RECOMPILED_SOURCES} PROPERTIES
                COMPILE_OPTIONS "/O1")
        else()
            set_source_files_properties(${GAME_RECOMPILED_SOURCES} PROPERTIES
                COMPILE_OPTIONS "-O1;-g0")
        endif()
    endif()

    target_include_directories(SonicAdvanceRecomp PRIVATE
        "${GBARECOMP_ROOT}/src/armv4t"
        "${GBARECOMP_ROOT}/src/gba"
        "${GBARECOMP_ROOT}/src/runtime"
        "${GBARECOMP_ROOT}/src/debug"
        src)

    target_link_libraries(SonicAdvanceRecomp PRIVATE
        "-Wl,--start-group"
        gbarecomp_runtime
        gbarecomp_debug
        gbarecomp_gba
        gbarecomp_armv4t
        "-Wl,--end-group")

    target_compile_definitions(SonicAdvanceRecomp PRIVATE
        GBARECOMP_DEFAULT_DEBUG_PORT=19912
        GBARECOMP_WINDOW_TITLE="SonicAdvanceRecomp"
        GBARECOMP_DEFAULT_GAME_CONFIG="game.toml"
        GBARECOMP_BUILTIN_NAME="Sonic Advance"
        GBARECOMP_BUILTIN_SHA1="d842afa7dd1e84de08addb94a51506f1bcafd551"
        GBARECOMP_BUILTIN_CRC32=0x63F70FD8
        GBARECOMP_BUILTIN_REGION="USA")

    if(MSVC)
        target_link_options(SonicAdvanceRecomp PRIVATE /STACK:16777216)
    elseif(MINGW OR WIN32)
        target_link_options(SonicAdvanceRecomp PRIVATE -Wl,--stack,16777216)
    endif()

    # The runner config is copied for a developer build. The self-hosted
    # packaging workflow deliberately omits it so the first-run launcher asks
    # the player to select their own ROM and BIOS instead of shipping paths
    # tied to the developer's machine.
    add_custom_command(TARGET SonicAdvanceRecomp POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            "${CMAKE_CURRENT_SOURCE_DIR}/game.toml"
            "$<TARGET_FILE_DIR:SonicAdvanceRecomp>/game.toml"
        COMMAND ${CMAKE_COMMAND} -E make_directory
            "$<TARGET_FILE_DIR:SonicAdvanceRecomp>/saves"
        VERBATIM)
endif()

# MinishCapRecomp-style pre-boot launcher. Only this small translation unit
# receives recomp-ui's launcher compile definitions; generated guest code
# remains isolated from the UI build flags.
if(GBAGAME_RECOMP_UI)
    if(NOT EXISTS "${RECOMP_UI_ROOT}/recomp_ui.cmake")
        message(FATAL_ERROR
            "recomp-ui is missing at ${RECOMP_UI_ROOT}. "
            "Run: git submodule update --init --recursive")
    endif()

    include("${RECOMP_UI_ROOT}/recomp_ui.cmake")

    add_library(sonic_advance_launcher_ui STATIC src/game_launcher_boot.cpp)
    target_include_directories(sonic_advance_launcher_ui PRIVATE
        "${GBARECOMP_ROOT}/src/runtime"
        "${GBARECOMP_ROOT}/src/gba"
        src)

    # Adds recomp-ui sources, RECOMP_LAUNCHER compile definitions and assets.
    recomp_target_launcher_ui(sonic_advance_launcher_ui CONSOLE gba)

    find_package(SDL2 REQUIRED)
    target_link_libraries(sonic_advance_launcher_ui PUBLIC SDL2::SDL2)
    if(WIN32)
        target_link_libraries(sonic_advance_launcher_ui PUBLIC opengl32)
    endif()

    if(SONIC_BUILD_GAME_EXECUTABLE)
        target_link_libraries(SonicAdvanceRecomp PRIVATE sonic_advance_launcher_ui)
        set_property(SOURCE src/main.cpp APPEND PROPERTY
            COMPILE_DEFINITIONS GBAGAME_RECOMP_UI)
    endif()
endif()
