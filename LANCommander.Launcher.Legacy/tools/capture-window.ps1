# capture-window.ps1 — launch the launcher, screenshot its window, kill it.
#
# Used to eyeball a backend swap and to capture golden images: run it against
# the Allegro build and the SDL build and diff the PNGs.
#
# Usage: tools/capture-window.ps1 -Exe <path> -Out <png> [-Seconds 6]
param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$Seconds = 6
)

Add-Type -AssemblyName System.Drawing

Add-Type @'
using System;
using System.Runtime.InteropServices;
public class Win32Cap {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
}
'@

# Without this the capture rect comes back scaled by the desktop DPI factor
# and we screenshot the wrong region entirely.
[void][Win32Cap]::SetProcessDPIAware()

$exeFull = (Resolve-Path $Exe).Path
$workDir = Split-Path -Parent $exeFull

$env:PATH = "C:\msys64\mingw32\bin;$env:PATH"
$proc = Start-Process -FilePath $exeFull -WorkingDirectory $workDir -PassThru
Start-Sleep -Seconds $Seconds

if ($proc.HasExited) {
    Write-Error ("process exited early, code=0x{0:X}" -f $proc.ExitCode)
    exit 1
}

$proc.Refresh()
$hwnd = $proc.MainWindowHandle
if ($hwnd -eq [IntPtr]::Zero) {
    Stop-Process -Id $proc.Id -Force
    Write-Error "no main window handle"
    exit 1
}

# Windows often refuses SetForegroundWindow from a background process, which
# silently leaves another window on top and screenshots that instead. Force it
# topmost, then restore.
[void][Win32Cap]::SetWindowPos($hwnd, [IntPtr](-1), 0, 0, 0, 0, 0x0002 -bor 0x0001) # HWND_TOPMOST|NOMOVE|NOSIZE
[void][Win32Cap]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 1200

$r = New-Object Win32Cap+RECT
[void][Win32Cap]::GetWindowRect($hwnd, [ref]$r)
$w = $r.Right - $r.Left
$h = $r.Bottom - $r.Top

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$bmp.Save((Join-Path (Get-Location) $Out), [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

Stop-Process -Id $proc.Id -Force
Write-Output "captured ${w}x${h} -> $Out"
