function Write-ReplaceContentInFile([string]$Regex, [string]$Replacement, [string]$FilePath)
{
    $content = (Get-Content $FilePath) -replace $Regex, $Replacement
    [IO.File]::WriteAllLines($FilePath, $content)
}