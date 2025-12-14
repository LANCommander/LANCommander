---
title: Install
---

# Overview
LANCommander supports the use of install scripts for handling anything after the download and extraction of a game by the launcher or SDK. Common uses for install scripts include importing registry keys, updating configs, and templating file structures. Like all client-side scripts with LANCommander, these are PowerShell scripts that are executed post-extraction using the built in runtime.

Here are some examples of some post-install automation you may want to consider:
- Import/create registry keys necessary for game execution
- Automatic detection of the primary display's resolution to update the game's graphics config
- Scaffolding game files structures (profile directories required by the game)
- Automatic installation of dependencies

## Variables
When an install script is executed, the following variables are available within the runtime:

|           Name            |                  Type                   |                          Description                          | Applicable To           |
|:-------------------------:|:---------------------------------------:|:-------------------------------------------------------------:|-------------------------|
| `$InstallDirectory`         | `string`                                  | The install directory the game archive has been extracted to  | Games                   |
| `$GameManifest`             | `LANCommander.SDK.GameManifest`           | The game manifest containing metadata about the game          | Games                   |
| `$DefaultInstallDirectory`  | `string`                                  | The default install directory as specified in the client      | Games                   |
| `$ServerAddress`            | `string`                                  | The source LANCommander server address                        | Games, Redistributables |
| `$Redistributable`          | `LANCommander.SDK.Models.Redistributable` | The redistributable's data object                             | Redistributables        |