# Accessible via $Display.ScreenWidth and $Display.ScreenHeight
$Display = Get-WmiObject -Class Win32_DesktopMonitor | Select-Object ScreenWidth,ScreenHeight