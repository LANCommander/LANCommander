<#
.SYNOPSIS
    Finding and driving MSYS2 from PowerShell.
.DESCRIPTION
    Shared by build-win9x.ps1 and build-dos.ps1. Dot-source it:

        . "$PSScriptRoot\tools\Msys2.ps1"

    Both retro targets are cross builds whose toolchains are POSIX-shaped:
    Watt-32 configures with a shell script and builds with GNU make, and the
    Win9x import and instruction-set gates are shell scripts over objdump.
    Reimplementing all of that in PowerShell would be a second copy of the
    build to keep correct, so these wrappers drive the real scripts instead
    and their job is to hand MSYS2 the right environment.
#>

Set-StrictMode -Version Latest

<#
.SYNOPSIS
    Locates an MSYS2 installation, or returns $null.
.DESCRIPTION
    In order: an explicit path, the MSYS2_ROOT environment variable, the
    installer's registry entry, then the usual install locations.

    Deliberately does NOT look for bash.exe on PATH. On a machine with WSL
    enabled -- which is most of them now -- that finds C:\Windows\System32\bash.exe,
    which is a Linux shell in a different filesystem with no MSYS2, no MinGW
    and no view of the drive letters the build uses. It would get several
    steps in before failing in a way that names none of that.
#>
function Find-Msys2Root {
    [CmdletBinding()]
    param(
        [string] $ExplicitRoot
    )

    $candidates = New-Object System.Collections.Generic.List[string]

    if ($ExplicitRoot) { $candidates.Add($ExplicitRoot) }
    if ($env:MSYS2_ROOT) { $candidates.Add($env:MSYS2_ROOT) }

    $uninstallKeys = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*'
    )

    foreach ($entry in Get-ItemProperty $uninstallKeys -ErrorAction SilentlyContinue) {
        $name = $entry.PSObject.Properties['DisplayName']
        $location = $entry.PSObject.Properties['InstallLocation']

        if ($name -and $location -and $name.Value -like '*MSYS2*' -and $location.Value) {
            $candidates.Add($location.Value)
        }
    }

    foreach ($guess in @('C:\msys64', 'C:\msys32', 'C:\tools\msys64', "$env:SystemDrive\msys64")) {
        $candidates.Add($guess)
    }

    foreach ($candidate in $candidates) {
        if (-not $candidate) { continue }
        if (Test-Path (Join-Path $candidate 'usr\bin\bash.exe')) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

<#
.SYNOPSIS
    Stops with an install message naming exactly what is missing.
#>
function Assert-Msys2 {
    [CmdletBinding()]
    param(
        [string] $ExplicitRoot
    )

    $root = Find-Msys2Root -ExplicitRoot $ExplicitRoot

    if ($root) { return $root }

    Write-Host ''
    Write-Host 'ERROR: MSYS2 was not found.' -ForegroundColor Red
    Write-Host ''
    Write-Host '  Both retro builds cross-compile with toolchains that need a POSIX'
    Write-Host '  shell and GNU make. Install MSYS2 with one of:'
    Write-Host ''
    Write-Host '    winget install MSYS2.MSYS2'
    Write-Host '    choco install msys2'
    Write-Host '    https://www.msys2.org/'
    Write-Host ''
    Write-Host '  Then re-run this script. If MSYS2 is installed somewhere unusual,'
    Write-Host '  point at it with -Msys2Root or the MSYS2_ROOT environment variable.'
    Write-Host ''

    throw 'MSYS2 not found'
}

<#
.SYNOPSIS
    Converts a Windows path to the POSIX form MSYS2 expects.
.DESCRIPTION
    Through cygpath rather than by string surgery: MSYS2 maps more than drive
    letters, and a hand-rolled "C:\x" -> "/c/x" gets network paths and the
    /usr mount wrong.
#>
function ConvertTo-Msys2Path {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Msys2Root,
        [Parameter(Mandatory)] [string] $Path
    )

    $cygpath = Join-Path $Msys2Root 'usr\bin\cygpath.exe'
    $converted = & $cygpath -u $Path

    if ($LASTEXITCODE -ne 0 -or -not $converted) {
        throw "cygpath could not convert '$Path'"
    }

    return $converted.Trim()
}

<#
.SYNOPSIS
    True when every named file exists under the MSYS2 root.
.DESCRIPTION
    Used to check a build's prerequisites up front. Paths are relative to the
    MSYS2 root, e.g. 'mingw32\bin\gcc.exe'.
#>
function Test-Msys2Files {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Msys2Root,
        [Parameter(Mandatory)] [string[]] $RelativePaths
    )

    foreach ($relative in $RelativePaths) {
        if (-not (Test-Path (Join-Path $Msys2Root $relative))) {
            return $false
        }
    }

    return $true
}

<#
.SYNOPSIS
    Runs a command in an MSYS2 login shell.
.DESCRIPTION
    Does NOT return the exit code, deliberately. A PowerShell function returns
    everything written to the output stream, so returning a code from a
    function that also runs a build would hand the caller the entire build log
    with the number on the end. Read $LASTEXITCODE after calling instead -- it
    is an automatic global and survives the call, and letting the child's
    output flow down the pipeline untouched is also what keeps
    "build-dos.ps1 > log.txt" working.

    A LOGIN shell (-l), because that is what sources /etc/profile and builds
    the PATH for the chosen MSYSTEM -- without it /mingw32/bin is absent and
    cmake, gcc and make are all missing.

    CHERE_INVOKING keeps the working directory instead of dropping to the
    MSYS2 home, so relative paths in the called script still mean what the
    caller meant.

    MSYS2_PATH_TYPE is left alone (MSYS2's default, "minimal"), so the
    Windows PATH is not inherited. Windows Git, CMake and Perl on a
    developer's PATH otherwise shadow the MSYS2 ones and produce builds that
    work on one machine and not the next.
#>
function Invoke-Msys2Bash {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $Msys2Root,
        [Parameter(Mandatory)] [string] $Msystem,
        [Parameter(Mandatory)] [string] $Command,
        [string] $WorkingDirectory
    )

    $bash = Join-Path $Msys2Root 'usr\bin\bash.exe'

    if (-not (Test-Path $bash)) {
        throw "No bash at $bash"
    }

    $previousMsystem = $env:MSYSTEM
    $previousChere = $env:CHERE_INVOKING
    $previousLocation = Get-Location

    try {
        if ($WorkingDirectory) { Set-Location $WorkingDirectory }

        $env:MSYSTEM = $Msystem
        $env:CHERE_INVOKING = '1'

        & $bash -l -c $Command
    }
    finally {
        $env:MSYSTEM = $previousMsystem
        $env:CHERE_INVOKING = $previousChere
        Set-Location $previousLocation
    }
}

<#
.SYNOPSIS
    Shell-quotes a string for bash.
.DESCRIPTION
    Single quotes, with any embedded single quote closed and reopened. Build
    trees live under paths like "C:\Users\Pat O'Brien\src", and an unquoted
    argument there ends the build with a shell syntax error.
#>
function ConvertTo-BashQuoted {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $Value
    )

    return "'" + $Value.Replace("'", "'\''") + "'"
}
