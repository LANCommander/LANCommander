function Get-LastSemVerTag {
    [OutputType([semver])]
    param()
    
    git fetch --tags

    $lastTag = git tag --list | Where-Object { $_ -match '^v?\d+\.\d+\.\d+$' } | Sort-Object -Descending | Select-Object -First 1

    return [semver]$lastTag.TrimStart('v')
}