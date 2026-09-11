<#
.SYNOPSIS
    Builds the LANCommander Legacy Launcher for MS-DOS, from Windows.
.DESCRIPTION
    A PowerShell front end for build-dos.sh.

    The DJGPP cross compiler is itself a native Windows build, and so is
    CMake -- but Watt-32, the only TCP/IP stack DOS has, configures with a
    shell script and builds with GNU make. So the build runs inside MSYS2,
    which is also where the fetch steps get curl, unzip and git.

    Everything else is downloaded on first run into vendor/, which .gitignore
    covers: the DJGPP toolchain, the CWSDPMI DPMI host, and the Watt-32
    submodule. Expect the first build to take a while and later ones not to.
.PARAMETER BuildType
    Release (default) or Debug.
.PARAMETER Jobs
    Parallel compile jobs. Defaults to the processor count.
.PARAMETER Msys2Root
    Where MSYS2 lives, if it is not somewhere findable.
.EXAMPLE
    .\build-dos.ps1
.EXAMPLE
    .\build-dos.ps1 -BuildType Debug -Jobs 8
#>

[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug', 'RelWithDebInfo', 'MinSizeRel')]
    [string] $BuildType = 'Release',

    [ValidateRange(1, 256)]
    [int] $Jobs = 0,

    [string] $Msys2Root
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot\tools\Msys2.ps1"

if ($Jobs -le 0) {
    $Jobs = [Environment]::ProcessorCount
    if ($Jobs -le 0) { $Jobs = 2 }
}

$msys2 = Assert-Msys2 -ExplicitRoot $Msys2Root

Write-Host '=== LANCommander Legacy Launcher - MS-DOS build (Windows host) ==='
Write-Host "  MSYS2      : $msys2"
Write-Host "  Build type : $BuildType"
Write-Host "  Jobs       : $Jobs"
Write-Host ''

# ---------------------------------------------------------------------------
# Package preflight
# ---------------------------------------------------------------------------
# cmake comes from the MinGW 32-bit package rather than an MSYS one, which is
# why the shell below is MSYSTEM=MINGW32 even though nothing here is built
# with MinGW: the DJGPP compiler is a separate, native toolchain that CMake
# only drives.
$required = @{
    'mingw32\bin\cmake.exe' = 'mingw-w64-i686-cmake'
    'usr\bin\make.exe'      = 'make'
    'usr\bin\curl.exe'      = 'curl'
    'usr\bin\unzip.exe'     = 'unzip'
    'usr\bin\git.exe'       = 'git'
}

$missing = @()
foreach ($file in $required.Keys) {
    if (-not (Test-Msys2Files -Msys2Root $msys2 -RelativePaths @($file))) {
        $missing += $required[$file]
    }
}

if ($missing.Count -gt 0) {
    $packages = ($missing | Sort-Object -Unique) -join ' '

    Write-Host 'ERROR: MSYS2 is installed but the DOS build prerequisites are not.' -ForegroundColor Red
    Write-Host ''
    Write-Host '  Install them with:'
    Write-Host ''
    Write-Host "    & '$msys2\usr\bin\bash.exe' -lc 'pacman -S --needed --noconfirm $packages'"
    Write-Host ''
    throw 'Missing MSYS2 packages'
}

# A first run downloads a toolchain and builds a TCP/IP stack. Saying so is
# the difference between "it is working" and "it has hung".
$djgpp = Join-Path $PSScriptRoot 'vendor\djgpp\djgpp\bin\i586-pc-msdosdjgpp-gcc.exe'
if (-not (Test-Path $djgpp)) {
    Write-Host '  First run: the DJGPP toolchain, CWSDPMI and Watt-32 will be'
    Write-Host '  fetched and built. This takes several minutes.'
    Write-Host ''
}

# ---------------------------------------------------------------------------
# Hand off
# ---------------------------------------------------------------------------
$scriptPosix = ConvertTo-Msys2Path -Msys2Root $msys2 -Path (Join-Path $PSScriptRoot 'build-dos.sh')

$command = 'exec {0} {1} {2}' -f `
    (ConvertTo-BashQuoted $scriptPosix), `
    (ConvertTo-BashQuoted $BuildType), `
    (ConvertTo-BashQuoted "$Jobs")

Invoke-Msys2Bash -Msys2Root $msys2 -Msystem 'MINGW32' `
                 -Command $command -WorkingDirectory $PSScriptRoot

$exit = $LASTEXITCODE

if ($exit -ne 0) {
    Write-Host ''
    Write-Host "Build failed (exit $exit)." -ForegroundColor Red
    exit $exit
}

Write-Host ''
Write-Host "Output: $(Join-Path $PSScriptRoot 'out-dos')" -ForegroundColor Green
Write-Host 'Copy that directory to the DOS machine and run LANCMDR.EXE.'

exit 0
