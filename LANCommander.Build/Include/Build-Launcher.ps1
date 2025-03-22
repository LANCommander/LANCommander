function Build-Launcher {
    [CmdletBinding()]
    Param(
        [Parameter(Mandatory=$true)]
        [SemVer] $Version,
        [String] $Runtime,
        [String] $Architecture,
        [String] $Platform,
        [String] $Configuration
    )

    $AssemblyVersion = "$($Version.Major).$($Version.Minor).$($Version.Patch)"
    $RuntimeIdentifier = "$Runtime-$Architecture"

    dotnet restore

    npm install --prefix ./LANCommander.UI
    npm install --prefix ./LANCommander.Launcher

    dotnet publish ./LANCommander.AutoUpdater/LANCommander.AutoUpdater.csproj -c $Configuration --self-contained --runtime $RuntimeIdentifier -p:Version="$Version" -p:AssemblyVersion="$AssemblyVersion"
    dotnet publish ./LANCommander.Launcher/LANCommander.Launcher.csproj -c $Configuration --self-contained --runtime $RuntimeIdentifier -p:Version="$Version" -p:AssemblyVersion="$AssemblyVersion"
    dotnet publish ./LANCommander.Launcher.CLI/LANCommander.Launcher.CLI.csproj -c $Configuration --self-contained --runtime $RuntimeIdentifier -p:Version="$Version" -p:AssemblyVersion="$AssemblyVersion"

    Copy-Item -Force -Recurse -Verbose LANCommander.AutoUpdater/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/* LANCommander.Launcher/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/
    Copy-Item -Force -Recurse -Verbose LANCommander.Launcher.CLI/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/* LANCommander.Launcher/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/

    $PathsToRemove = @(
        'wwwroot/_content/BootstrapBlazor.PdfReader/compat',
        'wwwroot/_content/BootstrapBlazor.PdfReader/2.*',
        'wwwroot/_content/BootstrapBlazor.PdfReader/build/pdf.sandbox.js',
        'wwwroot/_content/BootstrapBlazor.PdfReader/build/*.map',
        'wwwroot/_content/BootstrapBlazor.PdfReader/web/*.map',
        'wwwroot/_content/AntDesign/less',
        'wwwroot/_content/BlazorMonaco/lib/monaco-editor/min-maps',
        'wwwroot/Identity/lib/bootstrap',
        'LANCommander.ico',
        'LANCommanderDark.ico',
        'package-lock.json',
        'package.json',
        '*.pdb',
        'hostfxr.dll.bak',
        'Libraries/locales'
    )
      
    $BasePath = "LANCommander.Launcher/bin/$Configuration/net9.0/$RuntimeIdentifier/publish"

    foreach ($path in $PathsToRemove) {
        Remove-Item -Force -Recurse -ErrorAction SilentlyContinue "$BasePath/$path"
    }

    $Compress = @{
        Path = "LANCommander.Launcher/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/*"
        DestinationPath = "LANCommander.Launcher-$Platform-$Architecture-v$Version.zip"
        CompressionLevel = "Fastest"
    }

    Compress-Archive @compress

    $Compress.Path
}