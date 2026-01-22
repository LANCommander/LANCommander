---
title: Key Change
---

# Overview
Key change scripts are executed on the client's machine whenever they install a game from the launcher or SDK. When a client requests a key from LANCommander, they are given a key that is not currently allocated. Keys can be allocated based on user account or MAC address by configuring the game's **Key Allocation Method**.

Handling a key change can vary from game to game. Many games opt to store their key in the registry. It was popular for publishers to work with developers to implement keys into games. As such, game published under companies like EA or Activision often used a standardized format to store keys on a machine. For instance, EA games made from 2002-2008 would store the game's CD key in the registry under a path of:
```
HKLM:\SOFTWARE\WOW6432Node\Electronic Arts\<Game>\ergc
```
This can be seen in games such as *The Lord of the Rings: The Battle for Middle-earth II - The Rise of the Witch-King* and *Command & Conquer: Generals*.

There are a few outliers that would store their CD key in plain text somewhere in the games installation directory. Or there are games such as *StarCraft* or *Warcraft III: The Frozen Throne* that would store the keys in a proprietary config file that could only be read or written to by the installers. For games like these you'll often find small tools dedicated to changing the key without reinstalling the game. For these key changing tools, it is recommended to use [AutoHotkey](https://www.autohotkey.com/) to help automate the process.

## Variables
When a key change script is executed, the following variables are available within the runtime:
|            Name            |              Type               |                          Description                          |
|:--------------------------:|:-------------------------------:|:-------------------------------------------------------------:|
| `$InstallDirectory`        | `string`                        | The install directory the game archive has been extracted to  |
| `$GameManifest`            | `LANCommander.SDK.GameManifest` | The game manifest containing metadata about the game          |
| `$DefaultInstallDirectory` | `string`                        | The default install directory as specified in the client      |
| `$ServerAddress`           | `string`                        | The source LANCommander server address                        |
| `$AllocatedKey`            | `string`                        | The key that has been allocated to the user/machine           |