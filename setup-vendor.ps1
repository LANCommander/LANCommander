<#
.SYNOPSIS
    Downloads vendor libraries for LANCommander.SDK.Cpp and LANCommander.Launcher.Legacy.
.DESCRIPTION
    Initialises the picoposh git submodule, then fetches the following
    dependencies from GitHub:
      - cJSON 1.7.19        -> LANCommander.SDK.Cpp/vendor/cjson/
      - miniz 3.1.0         -> LANCommander.Launcher.Legacy/vendor/miniz/
      - Allegro 4.4.3.1     -> LANCommander.Launcher.Legacy/vendor/allegro4/
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$RepoRoot = $PSScriptRoot

# ---------------------------------------------------------------------------
# Dependency definitions
# ---------------------------------------------------------------------------
$deps = @(
    @{
        Name    = 'cJSON 1.7.19'
        Url     = 'https://github.com/DaveGamble/cJSON/archive/refs/tags/v1.7.19.zip'
        Dest    = Join-Path $RepoRoot 'LANCommander.SDK.Cpp/vendor/cjson'
        ZipRoot = 'cJSON-1.7.19'
        Files   = @('cJSON.c', 'cJSON.h')
    },
    @{
        # Flattened into one directory: the SDK compiles the sources directly
        # rather than reproducing libyaml's include/ + src/ split.
        Name    = 'libyaml 0.2.5'
        Url     = 'https://github.com/yaml/libyaml/archive/refs/tags/0.2.5.zip'
        Dest    = Join-Path $RepoRoot 'LANCommander.SDK.Cpp/vendor/libyaml'
        ZipRoot = 'libyaml-0.2.5'
        Files   = @(
            'include/yaml.h', 'src/yaml_private.h',
            'src/api.c', 'src/dumper.c', 'src/emitter.c', 'src/loader.c',
            'src/parser.c', 'src/reader.c', 'src/scanner.c', 'src/writer.c',
            'License'
        )
        Flatten = $true
    },
    @{
        Name    = 'miniz 3.1.0'
        Url     = 'https://github.com/richgel999/miniz/archive/refs/tags/3.1.0.zip'
        Dest    = Join-Path $RepoRoot 'LANCommander.Launcher.Legacy/vendor/miniz'
        ZipRoot = 'miniz-3.1.0'
        Files   = @(
            'miniz.c', 'miniz.h',
            'miniz_common.h', 'miniz_export.h',
            'miniz_tdef.c', 'miniz_tdef.h',
            'miniz_tinfl.c', 'miniz_tinfl.h',
            'miniz_zip.c', 'miniz_zip.h'
        )
    },
    @{
        Name    = 'Allegro 4.4.3.1'
        Url     = 'https://github.com/liballeg/allegro5/archive/refs/tags/4.4.3.1.zip'
        Dest    = Join-Path $RepoRoot 'LANCommander.Launcher.Legacy/vendor/allegro4'
        ZipRoot = 'allegro5-4.4.3.1'
        Extract = 'full'
    }
)

# ---------------------------------------------------------------------------
# Helper: download and extract
# ---------------------------------------------------------------------------
function Get-VendorDep {
    param(
        [hashtable]$Dep
    )

    Write-Host "--- $($Dep.Name) ---" -ForegroundColor Cyan

    $tempZip = Join-Path ([System.IO.Path]::GetTempPath()) "lancommander_vendor_$([System.IO.Path]::GetRandomFileName()).zip"
    $tempDir = Join-Path ([System.IO.Path]::GetTempPath()) "lancommander_vendor_$([System.IO.Path]::GetRandomFileName())"

    try {
        # Download
        Write-Host "  Downloading $($Dep.Url)..."
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $Dep.Url -OutFile $tempZip -UseBasicParsing

        # Extract to temp
        Write-Host "  Extracting..."
        Expand-Archive -Path $tempZip -DestinationPath $tempDir -Force

        # Ensure destination exists
        if (-not (Test-Path $Dep.Dest)) {
            New-Item -ItemType Directory -Path $Dep.Dest -Force | Out-Null
        }

        $srcRoot = Join-Path $tempDir $Dep.ZipRoot

        if ($Dep.Extract -eq 'full') {
            # Copy the entire extracted directory
            $destSubDir = Join-Path $Dep.Dest $Dep.ZipRoot
            if (Test-Path $destSubDir) {
                Remove-Item -Recurse -Force $destSubDir
            }
            Copy-Item -Path $srcRoot -Destination $destSubDir -Recurse
            Write-Host "  Extracted to $destSubDir" -ForegroundColor Green

            # Also keep a copy of the zip for offline builds
            Copy-Item -Path $tempZip -Destination (Join-Path $Dep.Dest 'allegro-src.zip') -Force
        }
        else {
            # Copy only specified files. With -Flatten, a path like src/api.c
            # lands as api.c rather than recreating the archive's directories.
            foreach ($file in $Dep.Files) {
                $src = Join-Path $srcRoot $file
                if (-not (Test-Path $src)) {
                    Write-Warning "  File not found in archive: $file"
                    continue
                }

                $leaf = if ($Dep.Flatten) { Split-Path $file -Leaf } else { $file }
                $dst  = Join-Path $Dep.Dest $leaf

                $dstDir = Split-Path $dst -Parent
                if (-not (Test-Path $dstDir)) {
                    New-Item -ItemType Directory -Path $dstDir -Force | Out-Null
                }

                Copy-Item -Path $src -Destination $dst -Force
            }
            Write-Host "  Copied $($Dep.Files.Count) files to $($Dep.Dest)" -ForegroundColor Green
        }
    }
    finally {
        # Cleanup temp files
        if (Test-Path $tempZip)  { Remove-Item $tempZip -Force }
        if (Test-Path $tempDir)  { Remove-Item $tempDir -Recurse -Force }
    }
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
Write-Host "Setting up vendor dependencies..." -ForegroundColor Yellow
Write-Host ""

# ---------------------------------------------------------------------------
# picoposh is a git submodule, not a download — the submodule pin is the single
# source of truth for its version.
#
# Never recursive: picoposh also pins Watt-32, a large DOS-only TCP/IP stack we
# never compile. Its zlib and libzip submodules ARE wanted, though — without
# them Expand-Archive links a stub, so scripts cannot unpack archives.
# ---------------------------------------------------------------------------
$picoposh = Join-Path $RepoRoot 'LANCommander.SDK.Cpp/vendor/picoposh'
if (-not (Test-Path (Join-Path $picoposh 'include/picoposh.h'))) {
    Write-Host '--- picoposh (git submodule) ---' -ForegroundColor Cyan
    git -C $RepoRoot submodule update --init --depth 1 -- 'LANCommander.SDK.Cpp/vendor/picoposh'
    if ($LASTEXITCODE -ne 0) { throw 'Failed to initialise the picoposh submodule.' }
    Write-Host '  Initialised.' -ForegroundColor Green
}

if (-not (Test-Path (Join-Path $picoposh 'third_party/libzip/CMakeLists.txt'))) {
    Write-Host '--- picoposh zlib + libzip (for Expand-Archive) ---' -ForegroundColor Cyan
    git -C $picoposh submodule update --init --depth 1 -- 'third_party/zlib' 'third_party/libzip'
    if ($LASTEXITCODE -ne 0) {
        Write-Warning '  Could not initialise zlib/libzip; Expand-Archive will be a stub.'
    } else {
        Write-Host '  Initialised.' -ForegroundColor Green
    }
    Write-Host ""
}

foreach ($dep in $deps) {
    Get-VendorDep -Dep $dep
    Write-Host ""
}

Write-Host "All vendor dependencies downloaded successfully." -ForegroundColor Green
