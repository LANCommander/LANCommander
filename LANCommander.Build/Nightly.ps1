. .\Includes\Get-LastSemVerTag.ps1
. .\Includes\Build-Server.ps1
. .\Includes\Build-Launcher.ps1

$Version = Get-LastSemVerTag
$Version.PreReleaseLabel = "nightly"
$Version.BuildLabel = Get-Date -Format "yyyyMMddHHmm"

$BuildTargets = 
    @{ Platform = 'Windows'; $Architecture = 'x64';    $Runtime = 'win' },
    @{ Platform = 'Windows'; $Architecture = 'arm64';  $Runtime = 'win' },
    @{ Platform = 'Linux';   $Architecture = 'x64';    $Runtime = 'linux' },
    @{ Platform = 'Windows'; $Architecture = 'arm64';  $Runtime = 'linux' },
    @{ Platform = 'Windows'; $Architecture = 'x64';    $Runtime = 'osx' },
    @{ Platform = 'Windows'; $Architecture = 'arm64';  $Runtime = 'osx' }

Write-Host "Building Targets"

foreach ($target in $BuildTargets) {
    Build-Server -Version $Version -Runtime $target.Runtime -Architecture $target.Architecture -Platform $target.Platform -Configuration 'Debug'
    Build-Launcher -Version $Version -Runtime $target.Runtime -Architecture $target.Architecture -Platform $target.Platform -Configuration 'Debug'
}