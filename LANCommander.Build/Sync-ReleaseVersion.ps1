#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ReleaseTag,
    [string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'

# SemVer 2.0 identifiers: numeric prerelease identifiers cannot have leading zeroes.
$number = '(0|[1-9][0-9]*)'
$identifier = '(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)'
$pattern = "\Av?$number\.$number\.$number(?:-$identifier(?:\.$identifier)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z"
$tagMatch = [regex]::Match($ReleaseTag, $pattern)
if (-not $tagMatch.Success) {
    throw "Expected a SemVer release tag (vX.Y.Z or vX.Y.Z-prerelease), got '$ReleaseTag'."
}

$parts = foreach ($index in 1..3) {
    $component = 0
    if (-not [int]::TryParse($tagMatch.Groups[$index].Value, [ref]$component) -or $component -gt 65534) {
        throw "Release tag '$ReleaseTag' exceeds the .NET assembly version component range (0..65534)."
    }
    $component
}
$version = $ReleaseTag -creplace '\Av', ''
$assemblyVersion = ($parts -join '.') + '.0'
$path = Join-Path $RepositoryRoot 'Directory.Build.props'
$content = [System.IO.File]::ReadAllText($path)
$versionPattern = '<Version>([^<]+)</Version>'
$assemblyPattern = '<AssemblyVersion>([^<]+)</AssemblyVersion>'
foreach ($propertyPattern in @($versionPattern, $assemblyPattern)) {
    if ([regex]::Matches($content, $propertyPattern).Count -ne 1) {
        throw "Expected exactly one $propertyPattern in Directory.Build.props."
    }
}
$current = [regex]::Match($content, $versionPattern).Groups[1].Value
if (-not [regex]::IsMatch($current, $pattern) -or $current.StartsWith('v')) {
    throw "Directory.Build.props contains an invalid Version '$current'."
}

# Compare precedence, not strings: alpha.10 > alpha.2, and build metadata is ignored.
if ([semver]$current -gt [semver]$version) {
    Write-Host "Version $current is ahead of $version; not downgrading."
    return [pscustomobject]@{ Version = $version; AssemblyVersion = $assemblyVersion; Changed = $false }
}

$updated = [regex]::Replace($content, $versionPattern, "<Version>$version</Version>")
$updated = [regex]::Replace($updated, $assemblyPattern, "<AssemblyVersion>$assemblyVersion</AssemblyVersion>")
$changed = $updated -cne $content
if ($changed) {
    [System.IO.File]::WriteAllText($path, $updated)
    Write-Host "Synchronized Version=$version and AssemblyVersion=$assemblyVersion."
}
else {
    Write-Host "Version properties already match $ReleaseTag."
}
[pscustomobject]@{ Version = $version; AssemblyVersion = $assemblyVersion; Changed = $changed }
