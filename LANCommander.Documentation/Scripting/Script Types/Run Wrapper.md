---
title: Run Wrapper
---

# Overview
Run Wrapper scripts provide a way for [compatibility shim redistributables](/Server/Redistributables#compatibility-shims) to control how a game executable is launched. Unlike a [Command Template](/Server/Redistributables#commandtemplate), which simply rewrites the executable path and arguments, a Run Wrapper script has full control over the launch process and can perform complex operations like DLL injection, file copying, or environment setup before starting the game.

Run Wrapper scripts are defined on a redistributable, not on individual games. When a game is launched that has a redistributable with a Run Wrapper script, the script is executed instead of the normal process launch.

## Variables
When a Run Wrapper script is executed, the following variables are available within the runtime:

|           Name            |                  Type                   |                          Description                          |
|:-------------------------:|:---------------------------------------:|:-------------------------------------------------------------:|
| `$InstallDirectory`        | `string`                                  | The install directory of the game                            |
| `$GameManifest`             | `LANCommander.SDK.GameManifest`           | The game manifest containing metadata about the game         |
| `$ExecutablePath`           | `string`                                  | The resolved path to the game executable                     |
| `$Arguments`                | `string`                                  | The resolved command-line arguments for the executable       |
| `$WorkingDirectory`         | `string`                                  | The resolved working directory for the executable            |
| `$ServerAddress`            | `string`                                  | The source LANCommander server address                       |

Additionally, all resolved option values from the redistributable's option schema are available. Use the `Get-RedistributableOptions` cmdlet to access them as a structured object.

## Example
This example shows a Run Wrapper script for a DLL injection-based compatibility tool:

```powershell
# Copy the compatibility DLL to the game directory
$shimPath = Join-Path $InstallDirectory ".interposer"
if (Test-Path $shimPath) {
    Copy-Item "$shimPath\*.dll" -Destination $InstallDirectory -Force
}

# Get the configured options
$options = Get-RedistributableOptions -Path $InstallDirectory -Id $GameManifest.Id -Name "MyShim"

# Launch the game
Start-Process -FilePath $ExecutablePath -ArgumentList $Arguments -WorkingDirectory $WorkingDirectory -Wait
```
