function Separate-AsciiBytes([byte[]]$Data)
{
    $array = @()

    foreach ($byte in $Data)
    {
        $array += $byte
        $array += 0x00
    }

    return $array
}