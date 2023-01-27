# Trim a string down to a specified amount of characters
if ($NewName.Length -gt 10) {
    $NewName = $NewName.Substring(0, 10);
}