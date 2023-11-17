# Trim a string down to a specified amount of characters
if ($NewPlayerAlias.Length -gt 10) {
    $NewPlayerAlias = $NewPlayerAlias.Substring(0, 10);
}