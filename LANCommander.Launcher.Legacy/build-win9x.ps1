<#
.SYNOPSIS
    Builds the LANCommander Legacy Launcher for Windows 95/98, from Windows.
.DESCRIPTION
    A PowerShell front end for build-win9x.sh. The build itself is unchanged:
    this finds MSYS2, checks that the packages the build needs are actually
    installed, and hands the script an MSYS2 MinGW 32-bit login shell.

    That indirection is not laziness. The Win9x build ends with two gates --
    an import scan against a deny list and an instruction-set scan over
    objdump output -- that exist because both failure modes kill the launcher
    before main() runs and are miserable to diagnose on a machine with no
    debugger. They are shell scripts, and a PowerShell reimplementation would
    be a second copy to keep correct.

    Requirements, all from MSYS2:
        pacman -S --needed mingw-w64-i686-gcc mingw-w64-i686-cmake \
                           mingw-w64-i686-make make

    For a binary that will actually run on a pre-SSE2 CPU you also want the
    i586 toolchain from tools/build-i586-toolchain.sh. Without it MSYS2's
    stock libstdc++ faults on an SSE2 opcode during static construction --
    before main(). This script says which one it used.
.PARAMETER BuildType
    Release (default) or Debug.
.PARAMETER Jobs
    Parallel compile jobs. Defaults to the processor count.
.PARAMETER Backend
    allegro (default) or sdl3. See build-win9x.sh for what each needs on the
    target.
.PARAMETER Msys2Root
    Where MSYS2 lives, if it is not somewhere findable.
.EXAMPLE
    .\build-win9x.ps1
.EXAMPLE
    .\build-win9x.ps1 -BuildType Debug -Jobs 4 -Backend sdl3
#>

[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug', 'RelWithDebInfo', 'MinSizeRel')]
    [string] $BuildType = 'Release',

    [ValidateRange(1, 256)]
    [int] $Jobs = 0,

    [ValidateSet('allegro', 'sdl3')]
    [string] $Backend = 'allegro',

    [string] $Msys2Root
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\tools\Msys2.ps1"

if ($Jobs -le 0) {
    $Jobs = [Environment]::ProcessorCount
    if ($Jobs -le 0) { $Jobs = 2 }
}

$msys2 = Assert-Msys2 -ExplicitRoot $Msys2Root

Write-Host '=== LANCommander Legacy Launcher - Win9x build (Windows host) ==='
Write-Host "  MSYS2      : $msys2"
Write-Host "  Build type : $BuildType"
Write-Host "  Backend    : $Backend"
Write-Host "  Jobs       : $Jobs"

# ---------------------------------------------------------------------------
# Package preflight
# ---------------------------------------------------------------------------
# Checked here rather than left to the build, because a missing MinGW package
# surfaces halfway through CMake as a compiler-detection failure that names
# neither MSYS2 nor pacman.
$required = @{
    'mingw32\bin\gcc.exe'           = 'mingw-w64-i686-gcc'
    'mingw32\bin\g++.exe'           = 'mingw-w64-i686-gcc'
    'mingw32\bin\cmake.exe'         = 'mingw-w64-i686-cmake'
    'mingw32\bin\mingw32-make.exe'  = 'mingw-w64-i686-make'
}

$missing = @()
foreach ($file in $required.Keys) {
    if (-not (Test-Msys2Files -Msys2Root $msys2 -RelativePaths @($file))) {
        $missing += $required[$file]
    }
}

if ($missing.Count -gt 0) {
    $packages = ($missing | Sort-Object -Unique) -join ' '

    Write-Host ''
    Write-Host 'ERROR: MSYS2 is installed but the MinGW 32-bit toolchain is not.' -ForegroundColor Red
    Write-Host ''
    Write-Host '  Install it with:'
    Write-Host ''
    Write-Host "    & '$msys2\usr\bin\bash.exe' -lc 'pacman -S --needed --noconfirm $packages'"
    Write-Host ''
    throw 'Missing MSYS2 packages'
}

# The i586 runtime is what separates "builds" from "runs on the target", so
# say which one is in play before the build rather than after it.
$i586 = if ($env:WIN9X_TOOLCHAIN) { $env:WIN9X_TOOLCHAIN } else { '/opt/i586-win9x' }
$i586Windows = Join-Path $msys2 ($i586.TrimStart('/').Replace('/', '\'))

if (Test-Path (Join-Path $i586Windows 'bin\gcc.exe')) {
    Write-Host "  Toolchain  : $i586 (i586 runtime)"
}
else {
    Write-Host "  Toolchain  : stock MSYS2 - SSE2 runtime, will NOT run on a pre-SSE2 CPU" -ForegroundColor Yellow
    Write-Host "               build it with tools/build-i586-toolchain.sh" -ForegroundColor Yellow
}

Write-Host ''

# ---------------------------------------------------------------------------
# Hand off
# ---------------------------------------------------------------------------
$scriptPosix = ConvertTo-Msys2Path -Msys2Root $msys2 -Path (Join-Path $PSScriptRoot 'build-win9x.sh')

$command = 'exec {0} {1} {2} {3}' -f `
    (ConvertTo-BashQuoted $scriptPosix), `
    (ConvertTo-BashQuoted $BuildType), `
    (ConvertTo-BashQuoted "$Jobs"), `
    (ConvertTo-BashQuoted $Backend)

Invoke-Msys2Bash -Msys2Root $msys2 -Msystem 'MINGW32' `
                 -Command $command -WorkingDirectory $PSScriptRoot

$exit = $LASTEXITCODE

if ($exit -ne 0) {
    Write-Host ''
    Write-Host "Build failed (exit $exit)." -ForegroundColor Red
    exit $exit
}

$outputDir = if ($Backend -eq 'sdl3') { 'out-win9x-sdl3' } else { 'out-win9x' }
Write-Host ''
Write-Host "Output: $(Join-Path $PSScriptRoot $outputDir)" -ForegroundColor Green

exit 0
