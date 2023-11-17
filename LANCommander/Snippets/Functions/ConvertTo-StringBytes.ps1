# Converts a string to a UTF16-encoded byte array. This looks like ASCII characters separated by 0x00 in most cases.
$bytes = ConvertTo-StringBytes -Input "Hello World!" -Utf16 1 -MaxLength 12