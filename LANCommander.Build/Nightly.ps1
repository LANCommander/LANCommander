. ./LANCommander.Build/Include/Get-LastSemVerTag.ps1
. ./LANCommander.Build/Include/Build-Server.ps1
. ./LANCommander.Build/Include/Build-Launcher.ps1

$Version = Get-LastSemVerTag
$Version = [semver]"$($Version.Major).$($Version.Minor).$($Version.Patch)-nightly.$(Get-Date -Format "yyyyMMddHHmm")"

$BuildTargets = 
    @{ Platform = 'Windows'; Architecture = 'x64';   Runtime = 'win' },
    @{ Platform = 'Windows'; Architecture = 'arm64'; Runtime = 'win' },
    @{ Platform = 'Linux';   Architecture = 'x64';   Runtime = 'linux' },
    @{ Platform = 'Windows'; Architecture = 'arm64'; Runtime = 'linux' },
    @{ Platform = 'Windows'; Architecture = 'x64';   Runtime = 'osx' },
    @{ Platform = 'Windows'; Architecture = 'arm64'; Runtime = 'osx' }

Write-Host "Building Targets"

foreach ($target in $BuildTargets) {
    Build-Server -Version $Version -Runtime $target.Runtime -Architecture $target.Architecture -Platform $target.Platform -Configuration 'Debug'
    Build-Launcher -Version $Version -Runtime $target.Runtime -Architecture $target.Architecture -Platform $target.Platform -Configuration 'Debug'
}