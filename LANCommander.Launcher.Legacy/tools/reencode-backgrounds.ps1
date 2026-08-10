# reencode-backgrounds.ps1 — shrink the login background JPEGs.
#
# Why this is a prerequisite for the stb image stack, not an optimisation:
# GDI+ can be asked to decode a JPEG straight to a target size, but
# stbi_load_from_memory always materialises the image at full resolution
# before image_decoder's downscale runs. At 1920x1920 that is a single
# 14.1 MB RGBA allocation, on a machine that may only have 32 MB.
#
# Usage: tools/reencode-backgrounds.ps1 [-MaxDim 1280] [-Quality 82]
param(
    [int]$MaxDim = 1280,
    [int]$Quality = 82
)

Add-Type -AssemblyName System.Drawing

$dir = Join-Path (Split-Path -Parent $PSScriptRoot) "assets\backgrounds"
$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
         Where-Object { $_.MimeType -eq 'image/jpeg' }
$params = New-Object System.Drawing.Imaging.EncoderParameters(1)
$params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
    [System.Drawing.Imaging.Encoder]::Quality, [long]$Quality)

foreach ($f in Get-ChildItem (Join-Path $dir '*.jpg')) {
    $src = [System.Drawing.Image]::FromFile($f.FullName)
    $oldW = $src.Width; $oldH = $src.Height; $oldKB = [math]::Round($f.Length / 1KB)

    $scale = [math]::Min(1.0, $MaxDim / [math]::Max($src.Width, $src.Height))
    if ($scale -ge 1.0) {
        Write-Output ("{0}: already <= {1}px, skipped" -f $f.Name, $MaxDim)
        $src.Dispose()
        continue
    }

    $w = [int][math]::Round($src.Width * $scale)
    $h = [int][math]::Round($src.Height * $scale)

    $dst = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawImage($src, 0, 0, $w, $h)
    $g.Dispose()
    $src.Dispose()

    $tmp = "$($f.FullName).tmp"
    $dst.Save($tmp, $codec, $params)
    $dst.Dispose()

    Move-Item -Force $tmp $f.FullName
    $newKB = [math]::Round((Get-Item $f.FullName).Length / 1KB)

    Write-Output ("{0}: {1}x{2} {3}KB -> {4}x{5} {6}KB" -f `
        $f.Name, $oldW, $oldH, $oldKB, $w, $h, $newKB)
}
