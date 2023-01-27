function Get-AsciiBytes([string]$InputString, [int]$MaxLength)
{
    if ($InputString.Length -gt $MaxLength)
    {
        $InputString = $InputString.Substring(0, $MaxLength)
    }

    $bytes = [System.Text.Encoding]::ASCII.GetBytes($InputString)
    $array = @()
    $count = 0

    $extraPadding = $MaxLength - $bytes.Length

    foreach ($byte in $bytes)
    {
        if ($count -lt $MaxLength)
        {
            $array += $byte
            $count++
        }
    }

    # Pad the end with 0x00 to meet our max length
    for ($i = $count; $i -lt $MaxLength; $i++)
    {
        $array += 0x00
    }

    return $array
}