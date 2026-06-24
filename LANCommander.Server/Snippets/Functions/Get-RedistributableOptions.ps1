# Returns resolved options (defaults + per-game overrides) for a redistributable
$options = Get-RedistributableOptions -Path $InstallDirectory -Id $GameManifest.Id -Name $RedistributableManifest.Name