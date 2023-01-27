function Patch-Binary([byte[]]$Data, [int]$Offset, [string]$FilePath)
{
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)

    for ($i = 0; $i -lt $Data.Length; $i++)
    {
        $bytes[$Offset + $i] = $Data[$i]
    }

    [System.IO.File]::WriteAllBytes($FilePath, $bytes)
}