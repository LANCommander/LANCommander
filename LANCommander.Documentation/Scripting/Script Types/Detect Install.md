---
title: Detect Install
---

# Overview
LANCommander supports the use of Detect Install scripts when installing redistributables. This type of script only serves the purpose to tell the launcher or SDK if a redistributable is already installed/available on the client's machine. These are useful to avoid installing the same redistributable that may be used for multiple games.

The result of a detect install script must be the setting of the variable `$Return`. This should be a boolean value and should equal `$True` or `1` if the redistributable is installed, and `$False` or `0` if it is not.

## Example
This example script was written to detect the installation of DirectX:
```powershell
$Exists = Test-Path "HKLM:\\SOFTWARE\Microsoft\DirectX"

if ($Exists -eq $True) {
    $Return = $True
} else {
    $Return = $False
}
```

## Variables
When a detect install script is executed, the following variables are available within the script:
|        Name        |                   Type                    |               Description               |
|:------------------:|:-----------------------------------------:|:---------------------------------------:|
| `$Redistributable` | `LANCommander.SDK.Models.Redistributable` |  The redistributable's data object      |
| `$ServerAddress`   | `string`                                  | The source LANCommander server address  |