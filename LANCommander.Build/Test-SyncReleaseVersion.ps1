#requires -Version 7.0
$ErrorActionPreference = 'Stop'
$script = Join-Path $PSScriptRoot 'Sync-ReleaseVersion.ps1'
$root = Join-Path ([System.IO.Path]::GetTempPath()) "lancommander-version-$([guid]::NewGuid())"
$path = Join-Path $root 'Directory.Build.props'
$null = New-Item -ItemType Directory -Path $root

function Assert($Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Set-Fixture([string]$Version = '1.1.0', [string]$AssemblyVersion = '1.1.0.0') {
    $script:fixture = "<Project>`r`n  <PropertyGroup>`r`n    <Version>$Version</Version>`r`n    <AssemblyVersion>$AssemblyVersion</AssemblyVersion>`r`n    <Unrelated>unchanged</Unrelated>`r`n  </PropertyGroup>`r`n</Project>`r`n"
    [System.IO.File]::WriteAllText($path, $script:fixture)
}

function Assert-Rejected([string]$Tag) {
    $before = [System.IO.File]::ReadAllText($path)
    $rejected = $false
    try { $null = & $script -ReleaseTag $Tag -RepositoryRoot $root }
    catch { $rejected = $true }
    Assert $rejected "Expected rejection for '$Tag'."
    Assert ([System.IO.File]::ReadAllText($path) -ceq $before) "Rejected '$Tag' modified the file."
}

try {
    foreach ($case in @(
        @{ Tag = 'v2.2.0'; Version = '2.2.0'; Assembly = '2.2.0.0' },
        @{ Tag = '2.3.4'; Version = '2.3.4'; Assembly = '2.3.4.0' },
        @{ Tag = 'v2.2.0-alpha.3'; Version = '2.2.0-alpha.3'; Assembly = '2.2.0.0' },
        @{ Tag = 'v2.2.0-rc.1+build.007'; Version = '2.2.0-rc.1+build.007'; Assembly = '2.2.0.0' },
        @{ Tag = 'v2.2.0-0.01a.alpha-beta'; Version = '2.2.0-0.01a.alpha-beta'; Assembly = '2.2.0.0' },
        @{ Tag = 'v2.2.0+build.007'; Version = '2.2.0+build.007'; Assembly = '2.2.0.0' },
        @{ Tag = 'v65534.0.0'; Version = '65534.0.0'; Assembly = '65534.0.0.0' }
    )) {
        Set-Fixture
        $result = & $script -ReleaseTag $case.Tag -RepositoryRoot $root
        $expected = $fixture.Replace('<Version>1.1.0</Version>', "<Version>$($case.Version)</Version>").
            Replace('<AssemblyVersion>1.1.0.0</AssemblyVersion>', "<AssemblyVersion>$($case.Assembly)</AssemblyVersion>")
        Assert $result.Changed "Expected update for $($case.Tag)."
        Assert ($result.Version -ceq $case.Version) "Incorrect Version for $($case.Tag)."
        Assert ($result.AssemblyVersion -ceq $case.Assembly) "Incorrect AssemblyVersion for $($case.Tag)."
        Assert ([System.IO.File]::ReadAllText($path) -ceq $expected) "Unexpected file changes for $($case.Tag)."
        $bytes = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($path))
        $result = & $script -ReleaseTag $case.Tag -RepositoryRoot $root
        Assert (-not $result.Changed) "Not idempotent for $($case.Tag)."
        Assert ([Convert]::ToBase64String([System.IO.File]::ReadAllBytes($path)) -ceq $bytes) "Rerun changed bytes for $($case.Tag)."
    }

    foreach ($case in @(
        @{ Current = '2.2.0-alpha.2'; Tag = 'v2.2.0-alpha.10'; Changed = $true },
        @{ Current = '2.2.0-alpha.10'; Tag = 'v2.2.0-alpha.2'; Changed = $false },
        @{ Current = '2.2.0-alpha.3'; Tag = 'v2.2.0'; Changed = $true },
        @{ Current = '2.2.0'; Tag = 'v2.2.0-rc.1'; Changed = $false },
        @{ Current = '2.2.0'; Tag = 'v2.1.17'; Changed = $false },
        @{ Current = '3.0.0'; Tag = 'v2.2.0'; Changed = $false },
        @{ Current = '2.2.0+build.1'; Tag = 'v2.2.0+build.2'; Changed = $true }
    )) {
        Set-Fixture $case.Current '2.2.0.0'
        $result = & $script -ReleaseTag $case.Tag -RepositoryRoot $root
        Assert ($result.Changed -eq $case.Changed) "Incorrect precedence handling for $($case.Current) -> $($case.Tag)."
        if (-not $case.Changed) {
            Assert ([System.IO.File]::ReadAllText($path) -ceq $fixture) 'Downgrade modified the file.'
        }
    }

    Set-Fixture '2.2.0-alpha.3' '1.1.0.0'
    $result = & $script -ReleaseTag 'v2.2.0-alpha.3' -RepositoryRoot $root
    Assert ($result.Changed -and [System.IO.File]::ReadAllText($path).Contains('<AssemblyVersion>2.2.0.0</AssemblyVersion>')) 'Equal Version must still repair a stale assembly identity.'

    Set-Fixture
    [System.IO.File]::WriteAllText($path, $fixture.Replace("`r`n", "`n"))
    $null = & $script -ReleaseTag 'v2.2.0' -RepositoryRoot $root
    Assert (-not [System.IO.File]::ReadAllText($path).Contains("`r")) 'LF line endings were changed.'

    Set-Fixture
    foreach ($invalid in @(
        'nightly', 'nightly-server', 'libvlc-lgpl-3.0.23', 'v2.2', 'v2.2.0.0', 'V2.2.0',
        'v02.2.0', 'v2.02.0', 'v2.2.00', 'v2.2.0-01', 'v2.2.0-alpha.01', 'v2.2.0-',
        'v2.2.0-alpha..1', 'v2.2.0+', 'v2.2.0+build..1', 'v2.2.0+build_1',
        'v65535.0.0', 'v0.65535.0', 'v0.0.65535', 'v99999999999999999999.0.0',
        ' v2.2.0', "v2.2.0`n", 'v2.2.0;echo bad', 'v2.2.0-$(bad)'
    )) {
        Assert-Rejected $invalid
    }

    foreach ($malformed in @(
        '<Project><Version>1.1.0</Version></Project>',
        '<Project><Version>1.1.0</Version><Version>1.1.0</Version><AssemblyVersion>1.1.0.0</AssemblyVersion></Project>',
        '<Project><Version>1.1.0</Version><AssemblyVersion>1.1.0.0</AssemblyVersion><AssemblyVersion>1.1.0.0</AssemblyVersion></Project>',
        '<Project><Version>invalid</Version><AssemblyVersion>1.1.0.0</AssemblyVersion></Project>'
    )) {
        [System.IO.File]::WriteAllText($path, $malformed)
        Assert-Rejected 'v2.2.0'
    }

    $repository = Split-Path $PSScriptRoot -Parent
    foreach ($project in @('LANCommander.Launcher', 'LANCommander.Server.UI')) {
        $projectPath = Join-Path (Join-Path $repository $project) "$project.csproj"
        $projectContent = [System.IO.File]::ReadAllText($projectPath)
        Assert (-not [regex]::IsMatch($projectContent, '<(?:Version|AssemblyVersion)>')) "$project must inherit version properties from Directory.Build.props."
    }
    Write-Host 'All release version synchronization tests passed.'
}
finally {
    Remove-Item -LiteralPath $root -Recurse -Force
}
