---
title: Name Change
---

# Overview
Name change scripts are executed on the client's machine whenever they change their name from the launcher or directly after the [install script](/Documentation/Scripting/InstallScripts) has executed. These scripts are dedicated solely to renaming a player's in-game name to ensure a consistency in multiplayer games.

There are a few guidelines that are recommended to follow when implementing a name change script for a game:
- The script should only replace the current name and should not touch anything outside of the player's profile
- Avoid leaving residual files on name changes. For instance, some games may have an entire file dedicated to a player's profile. This file should be renamed/altered to reflect the new name instead of a straight copy to a new profile.
- Many games have a limit on the amount of characters a player name can have. Make sure to trim or pad your player names if required. This is especially crucial for games that store the player name in a binary file. Read [this page](/Documentation/Scripting/Cmdlets) for helper cmdlets that may help in these scenarios.

Handling a name change primarily depends on how the game handles player saves. Game engines that store player names in plain text (e.g. Source, id Tech X, Unreal, etc.) are easily replaceable by using regular expressions (regex). Older games may have save files that need a binary patch. Others may just use a registry key.

The player's name is tracked using the file `$InstallDirectory\.lancommander\<Game ID>\PlayerAlias` within a game's install directory. This file is read on each game launch to verify if the player's name has changed. If it has, the name change script will execute. This allows LANCommander to maintain player name consistency.

## Variables
When a name change script is executed, the following variables are available within the runtime:
|            Name            |              Type               |                          Description                          |
|:--------------------------:|:-------------------------------:|:-------------------------------------------------------------:|
| `$InstallDirectory`        | `string`                        | The install directory the game archive has been extracted to  |
| `$GameManifest`            | `LANCommander.SDK.GameManifest` | The game manifest containing metadata about the game          |
| `$DefaultInstallDirectory` | `string`                        | The default install directory as specified in the client      |
| `$ServerAddress`           | `string`                        | The source LANCommander server address                        |
| `$OldPlayerAlias`          | `string`                        | The player's alias before the name change (if available)      |
| `$NewPlayerAlias`          | `string`                        | The player's alias after the name change                      |