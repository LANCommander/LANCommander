# Accessible via $Display.Width and $Display.Height
Add-Type -AssemblyName System.Windows.Forms
$Display = [System.Windows.Forms.Screen]::AllScreens | Where-Object Primary | Select Bounds