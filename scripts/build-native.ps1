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
        throw "Missing '$name'. Add the selected MSYS2 MINGW64 or UCRT64 bin folder and C:\msys64\usr\bin to PATH and try again."
    }
}

# This game ships adaptive widescreen as the launcher default on first run.
# Keep it as a normal saved launcher option so players can turn it off later.
$launcherSeam = Join-Path $root 'gbarecomp\\src\\runtime\\launcher_seam.h'
if (-not (Test-Path -LiteralPath $launcherSeam)) {
    throw "Pinned GBARecomp launcher seam not found: $launcherSeam"
}
$seamText = [System.IO.File]::ReadAllText($launcherSeam)
$seamPattern = '(?m)^(\\s*int\\s+adaptive_view\\s*=\\s*)0(\\s*;.*)# The ROM-derived sources are fully generated before configure. Suppress Ninja's
# automatic CMake regeneration loop; the next Builder run explicitly reconfigures.
cmake -S $root -B $buildDir -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=gcc -DCMAKE_CXX_COMPILER=g++ -DCMAKE_SUPPRESS_REGENERATION=ON
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration of SonicAdvanceRecomp failed.' }

cmake --build $buildDir --target SonicAdvanceRecomp --parallel 2
if ($LASTEXITCODE -ne 0) { throw 'Building SonicAdvanceRecomp failed.' }

$exe = Join-Path $buildDir 'SonicAdvanceRecomp.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Expected runner was not produced: $exe" }

Write-Host "Built: $exe"
Write-Host 'This is an initial integration build, not yet a validated playable port.'

$patchedSeamText = [System.Text.RegularExpressions.Regex]::Replace(
    $seamText, $seamPattern,
    [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + '1' + $m.Groups[2].Value }, 1)
if ($patchedSeamText -eq $seamText -and $seamText -notmatch '(?m)^\\s*int\\s+adaptive_view\\s*=\\s*1\\s*;') {
    throw 'Could not enable adaptive widescreen as the first-run launcher default; pinned launcher_seam.h may have changed.'
}
if ($patchedSeamText -ne $seamText) {
    [System.IO.File]::WriteAllText($launcherSeam, $patchedSeamText, [System.Text.UTF8Encoding]::new($false))
    Write-Host 'Adaptive widescreen: enabled by default; toggle remains available in the launcher.'
} else {
    Write-Host 'Adaptive widescreen launcher default already enabled.'
}

$buildDir = Join-Path $root 'build-native'
# The ROM-derived sources are fully generated before configure. Suppress Ninja's
# automatic CMake regeneration loop; the next Builder run explicitly reconfigures.
cmake -S $root -B $buildDir -G Ninja -DCMAKE_BUILD_TYPE=Release -DCMAKE_C_COMPILER=gcc -DCMAKE_CXX_COMPILER=g++ -DCMAKE_SUPPRESS_REGENERATION=ON
if ($LASTEXITCODE -ne 0) { throw 'CMake configuration of SonicAdvanceRecomp failed.' }

cmake --build $buildDir --target SonicAdvanceRecomp --parallel 2
if ($LASTEXITCODE -ne 0) { throw 'Building SonicAdvanceRecomp failed.' }

$exe = Join-Path $buildDir 'SonicAdvanceRecomp.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw "Expected runner was not produced: $exe" }

Write-Host "Built: $exe"
Write-Host 'This is an initial integration build, not yet a validated playable port.'
