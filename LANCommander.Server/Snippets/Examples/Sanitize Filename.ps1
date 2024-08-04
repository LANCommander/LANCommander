# Takes the variable $Filename, splits it by invalid characters, then joins using _
$Filename.Split([IO.Path]::GetInvalidFileNameChars()) -join '_'