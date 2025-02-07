# Elevate if not running as admin
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Start-Process -FilePath "powershell" -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

# Define service details
$serviceName = "LANCommander Server"
$serviceDisplayName = "Service for running the LANCommander Server"
$exePath = ".\LANCommander.Server.exe"

# Remove existing service
if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Remove-Service -Name $serviceName
    Start-Sleep -Seconds 2
}

# Install the service
New-Service -Name $serviceName -BinaryPathName "`"$exePath`"" -DisplayName $serviceDisplayName -StartupType Automatic

# Start the service
Start-Service -Name $serviceName

Write-Host "Service '$serviceName' installed and started successfully."