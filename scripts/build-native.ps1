param(
    [string]$Rom = (Join-Path $PSScriptRoot '..\roms\sonic_advance_usa.gba'),
    [string]$Bios = (Join-Path $PSScriptRoot '..\gbarecomp\bios\gba_bios.bin')
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$romPath = (Resolve-Path -LiteralPath $Rom).Path
$biosPath = (Resolve-Path -LiteralPath $Bios).Path

& (Join-Path $PSScriptRoot 'regen.ps1') -Rom $romPath -Bios $biosPath
if ($LASTEXITCODE -ne 0) { throw 'Regenerating Sonic Advance code failed.' }

foreach ($name in @('cmake', 'ninja', 'gcc', 'g++')) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "Missing '$name'. Add C:\msys64\mingw64\bin to PATH and try again."
    }
}

$buildDir = Join-Path $root 'build'
cmake -S $root -B $buildDir -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=gcc -DCMAKE_CXX_COMPILER=g++
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration of SonicAdvanceRecomp failed.' }

cmake --build $buildDir --target SonicAdvanceRecomp --parallel 2
if ($LASTEXITCODE -ne 0) { throw 'Building SonicAdvanceRecomp failed.' }

$exe = Join-Path $buildDir 'SonicAdvanceRecomp.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Expected runner was not produced: $exe" }

Write-Host "Built: $exe"
Write-Host 'This is an initial integration build, not yet a validated playable port.'
