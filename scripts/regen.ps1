param(
    [string]$Rom = (Join-Path $PSScriptRoot '..\roms\sonic_advance_usa.gba'),
    [string]$Bios = (Join-Path $PSScriptRoot '..\gbarecomp\bios\gba_bios.bin'),
    [int]$MaxFunctions = 65536
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

if (-not (Test-Path -LiteralPath (Join-Path $root 'gbarecomp\CMakeLists.txt'))) {
    git -C $root submodule update --init --recursive gbarecomp
    if ($LASTEXITCODE -ne 0) { throw 'Failed to initialize the pinned GBARecomp submodule.' }
}

$engine = (Resolve-Path (Join-Path $root 'gbarecomp')).Path
$romPath = (Resolve-Path -LiteralPath $Rom).Path
$biosPath = (Resolve-Path -LiteralPath $Bios).Path
$configPath = Join-Path $root 'game.toml'
$buildDir = Join-Path $engine 'build-native'
$tool = Join-Path $buildDir 'gba_recompile.exe'

$requiredCommands = @('cmake', 'ninja', 'gcc', 'g++')
foreach ($name in $requiredCommands) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Missing '$name'. Install the MSYS2 MINGW64 C++ toolchain and add C:\msys64\mingw64\bin to PATH."
    }
}

$expectedRomSha1 = 'd842afa7dd1e84de08addb94a51506f1bcafd551'
$actualRomSha1 = (Get-FileHash -LiteralPath $romPath -Algorithm SHA1).Hash.ToLowerInvariant()
if ($actualRomSha1 -ne $expectedRomSha1) {
    throw "Wrong ROM revision. Expected Sonic Advance USA (ASOE) SHA-1 $expectedRomSha1; got $actualRomSha1. No ROM was uploaded."
}

$expectedBiosSha1 = '300c20df6731a33952ded8c436f7f186d25d3492'
$actualBiosSha1 = (Get-FileHash -LiteralPath $biosPath -Algorithm SHA1).Hash.ToLowerInvariant()
if ($actualBiosSha1 -ne $expectedBiosSha1) {
    throw "GBA BIOS SHA-1 mismatch. Expected $expectedBiosSha1; got $actualBiosSha1."
}

if (-not (Test-Path -LiteralPath $tool)) {
    cmake -S $engine -B $buildDir -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=gcc -DCMAKE_CXX_COMPILER=g++ -DGBARECOMP_COMPILER_CACHE=OFF
    if ($LASTEXITCODE -ne 0) { throw 'CMake configuration of GBARecomp failed.' }
    cmake --build $buildDir --target gba_recompile --parallel 2
    if ($LASTEXITCODE -ne 0) { throw 'Building gba_recompile failed.' }
}
if (-not (Test-Path -LiteralPath $tool)) {
    throw "The GBARecomp compiler was not created at $tool."
}

$biosConfig = Join-Path $engine 'bios\gba_bios.toml'
$generatedBios = Join-Path $engine 'src\runtime\generated_bios'
New-Item -ItemType Directory -Force -Path $generatedBios | Out-Null
& $tool --bios $biosPath --config $biosConfig --out $generatedBios
if ($LASTEXITCODE -ne 0) { throw 'GBA BIOS recompilation failed.' }

$generated = Join-Path $root 'generated'
New-Item -ItemType Directory -Force -Path $generated | Out-Null
& $tool --rom $romPath --config $configPath --out $generated --max-functions $MaxFunctions
if ($LASTEXITCODE -ne 0) { throw 'Sonic Advance static code generation failed.' }

$gameSources = @(Get-ChildItem -LiteralPath $generated -File | Where-Object { $_.Extension -in '.c', '.cpp' })
if ($gameSources.Count -eq 0) { throw 'gba_recompile completed without emitting any .c/.cpp source files.' }

Write-Host "Generated $($gameSources.Count) source files for Sonic Advance USA."
Write-Host 'These ROM-derived files stay local and must not be committed.'
