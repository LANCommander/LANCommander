function Get-43Resolution([int]$Width, [int]$Height) {
    $ratio = 4 / 3

    if (($Width -gt $Height) -or ($Width -eq $Height)) {
        return @{ Width = [math]::Round($ratio * $Height); Height = $Height }
    }

    if ($Width -lt $Height) {
        return @{ Width = $Width; Height = [math]::Round($Width / $ratio) }
    }
}

# Accessible via $Resolution.Height, $Resolution.Width
$Resolution = Get-43Resolution -Width 1280 -Height 800