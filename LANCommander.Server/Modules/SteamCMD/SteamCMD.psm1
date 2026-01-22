Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-SteamCmd {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $SteamCmdPath,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [Parameter()] [string] $LogPath = ""
    )

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $SteamCmdPath
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true

    foreach ($a in $Arguments) { [void]$psi.ArgumentList.Add($a) }

    $proc = [System.Diagnostics.Process]::new()
    $proc.StartInfo = $psi

    [void]$proc.Start()

    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()

    $proc.WaitForExit()

    $combined = @(
        "===== SteamCMD STDOUT ====="
        $stdout
        "===== SteamCMD STDERR ====="
        $stderr
    ) -join "`n"

    if ($LogPath) {
        $dir = Split-Path -Parent $LogPath
        if ($dir) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Set-Content -LiteralPath $LogPath -Value $combined -Encoding UTF8
    }

    [PSCustomObject]@{
        ExitCode = $proc.ExitCode
        StdOut   = $stdout
        StdErr   = $stderr
        Combined = $combined
    }
}

function Install-SteamGame {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [int] $AppId,

        [Parameter(Mandatory)]
        [string] $GameName,

        [Parameter(Mandatory)]
        [string] $InstallRoot,

        [Parameter()]
        [string] $SteamCmdPath = "steamcmd",

        [Parameter()]
        [string] $Username = "anonymous",

        [Parameter()]
        [string] $Password = "",

        [Parameter()]
        [string] $Branch = "",

        [Parameter()]
        [string] $BranchPassword = "",

        [Parameter()]
        [switch] $Validate,

        [Parameter()]
        [switch] $IncludeSteamNews = $true
    )

    function Join-PathPortable([string] $A, [string] $B) {
        if ($A.EndsWith("/") -or $A.EndsWith("\")) { return "$A$B" }
        if ($A -match "^[a-zA-Z]:\\") { return "$A\$B" }
        return "$A/$B"
    }

    function Find-AppManifest([string] $InstallDir, [int] $AppIdValue) {
        $expected = Join-PathPortable $InstallDir ("steamapps/appmanifest_{0}.acf" -f $AppIdValue)
        if (Test-Path -LiteralPath $expected) { return $expected }

        Get-ChildItem -LiteralPath $InstallDir -Recurse -File `
            -Filter ("appmanifest_{0}.acf" -f $AppIdValue) `
            -ErrorAction SilentlyContinue |
            Select-Object -First 1 |
            ForEach-Object FullName
    }

    function Parse-AppManifestBuildInfo([string] $ManifestPath) {
        $content = Get-Content -LiteralPath $ManifestPath -Raw

        $buildId = [regex]::Match($content, '"buildid"\s+"(\d+)"').Groups[1].Value
        $lastUpdated = [regex]::Match($content, '"LastUpdated"\s+"(\d+)"').Groups[1].Value

        [PSCustomObject]@{
            BuildId = $buildId
            LastUpdatedUtc = if ($lastUpdated) {
                [DateTimeOffset]::FromUnixTimeSeconds([int64]$lastUpdated).UtcDateTime
            }
        }
    }

    function Get-SteamNewsChangelog([int] $AppIdValue) {
        try {
            $uri = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid=$AppIdValue&count=3&maxlength=800&format=json"
            $resp = Invoke-RestMethod -Uri $uri -TimeoutSec 15
            $resp.appnews.newsitems |
                ForEach-Object {
                    "[{0}] {1}`n{2}" -f `
                        ([DateTimeOffset]::FromUnixTimeSeconds($_.date).ToString("u")),
                        $_.title,
                        $_.contents
                } -join "`n`n"
        }
        catch {
            ""
        }
    }

    # ---- Install ----

    $installDir = Join-PathPortable $InstallRoot $GameName
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null

    $args = @(
        "+force_install_dir", $installDir,
        "+login", $Username
    )

    if ($Username -ne "anonymous" -and $Password) {
        $args += $Password
    }

    $args += "+app_update", $AppId

    if ($Branch) {
        $args += "-beta", $Branch
        if ($BranchPassword) {
            $args += "-betapassword", $BranchPassword
        }
    }

    if ($Validate) {
        $args += "validate"
    }

    $args += "+quit"

$logPath = Join-Path -Path $installDir -ChildPath "steamcmd.log"

$result = Invoke-SteamCmd -SteamCmdPath $SteamCmdPath -Arguments $args -LogPath $logPath

if ($result.ExitCode -ne 0) {
    $tail = ($result.Combined -split "`r?`n" | Select-Object -Last 80) -join "`n"
    throw @(
        "SteamCMD failed with exit code $($result.ExitCode)."
        "Log: $logPath"
        ""
        "Last 80 lines:"
        $tail
    ) -join "`n"
}


    # ---- Version / Changelog ----

    $manifest = Find-AppManifest -InstallDir $installDir -AppIdValue $AppId
    $build = if ($manifest) { Parse-AppManifestBuildInfo $manifest }

    $version = $build?.BuildId ?? "unknown"

    $changelog = if ($IncludeSteamNews) {
        Get-SteamNewsChangelog $AppId
    }

    if (-not $changelog) {
        $changelog = "BuildID: $version"
    }

    return [PSCustomObject]@{
        Path      = $installDir
        Version   = $version
        Changelog = $changelog
    }
}

Export-ModuleMember -Function Install-SteamGame