Get-ChildItem -Recurse -Filter *.csproj -File |
    Select-Object -Expand DirectoryName -Unique |
    ForEach-Object {
        foreach ($d in @('bin','obj')) {
            $p = Join-Path $_ $d
            if (Test-Path $p) { Remove-Item $p -Recurse -Force -ErrorAction SilentlyContinue }
        }
    }