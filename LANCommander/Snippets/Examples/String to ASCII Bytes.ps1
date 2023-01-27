# Convert an input string to ASCII-encoded byte[]. Shorter strings will pad out to 12 bytes, longer strings will be trimmed.
$bytes = Get-AsciiBytes -InputString "Hello world!" -MaxLength 12