function Build-Server {
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
    npm install --prefix ./LANCommander.Server

    dotnet publish ./LANCommander.AutoUpdater/LANCommander.AutoUpdater.csproj -c $Configuration --self-contained --runtime $RuntimeIdentifier -p:Version="$Version" -p:AssemblyVersion="$AssemblyVersion"
    dotnet publish ./LANCommander.Server/LANCommander.Server.csproj -c $Configuration --self-contained --runtime $RuntimeIdentifier -p:Version="$Version" -p:AssemblyVersion="$AssemblyVersion"

    Copy-Item -Force -Recurse -Verbose LANCommander.AutoUpdater/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/* LANCommander.Server/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/

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
      
    $BasePath = "LANCommander.Server/bin/$Configuration/net9.0/$RuntimeIdentifier/publish"

    foreach ($path in $PathsToRemove) {
        Remove-Item -Recurse -Force -ErrorAction Continue "$BasePath/$path"
    }

    $Compress = @{
        Path = "LANCommander.Server/bin/$Configuration/net9.0/$RuntimeIdentifier/publish/*"
        DestinationPath = "LANCommander.Server-$Platform-$Architecture-v$Version.zip"
        CompressionLevel = "Fastest"
    }

    Compress-Archive @compress

    $Compress.Path
}