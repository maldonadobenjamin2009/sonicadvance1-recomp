cmake_minimum_required(VERSION 3.20)
project(SonicAdvanceRecomp C CXX)

set(CMAKE_C_STANDARD 11)
set(CMAKE_C_STANDARD_REQUIRED ON)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(GBARECOMP_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/gbarecomp"
    CACHE PATH "Path to the pinned GBARecomp runtime checkout")

if(NOT EXISTS "${GBARECOMP_ROOT}/CMakeLists.txt")
    message(FATAL_ERROR
        "GBARecomp runtime is missing at ${GBARECOMP_ROOT}. "
        "Run: git submodule update --init --recursive")
endif()

if(NOT EXISTS "${CMAKE_CURRENT_SOURCE_DIR}/game.toml")
    message(FATAL_ERROR "game.toml is required beside the Sonic Advance project.")
endif()

add_subdirectory("${GBARECOMP_ROOT}" gbarecomp_build EXCLUDE_FROM_ALL)

file(GLOB GAME_GENERATED_SOURCES CONFIGURE_DEPENDS
    "${CMAKE_CURRENT_SOURCE_DIR}/generated/*.c"
    "${CMAKE_CURRENT_SOURCE_DIR}/generated/*.cpp")

if(NOT GAME_GENERATED_SOURCES)
    message(FATAL_ERROR
        "No runtime-compatible generated game source was found in generated/. "
        "Run scripts/regen.ps1 with your verified Sonic Advance USA ROM and GBA BIOS first. "
        "The output of the older standalone CLI (gbarecomp_game.lib) is not sufficient.")
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

# Keep the config beside the executable: the launcher runs the runner from its
# own directory, and game.toml contains relative asset/save paths.
add_custom_command(TARGET SonicAdvanceRecomp POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_if_different
        "${CMAKE_CURRENT_SOURCE_DIR}/game.toml"
        "$<TARGET_FILE_DIR:SonicAdvanceRecomp>/game.toml"
    COMMAND ${CMAKE_COMMAND} -E make_directory
        "$<TARGET_FILE_DIR:SonicAdvanceRecomp>/saves"
    VERBATIM)
