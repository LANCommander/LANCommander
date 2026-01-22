---
title: Uninstall
---

# Overview
LANCommander supports the use of uninstall scripts for handling any cleanup that may need to occur after the uninstallation of a game from the launcher or SDK. It is recommened to use uninstall scripts to remove any files and registry entries that might have been created from the install process and general execution of the game.

## Variables
When an uninstall script is executed, the following variables are available within the runtime:
|            Name            |              Type               |                          Description                          |
|:--------------------------:|:-------------------------------:|:-------------------------------------------------------------:|
| `$InstallDirectory`        | `string`                        | The install directory the game archive has been extracted to  |
| `$GameManifest`            | `LANCommander.SDK.GameManifest` | The game manifest containing metadata about the game          |
| `$DefaultInstallDirectory` | `string`                        | The default install directory as specified in the client      |
| `$ServerAddress`           | `string`                        | The source LANCommander server address                        |