---
title: Games
---

# Overview
:::warning
LANCommander does not provide or distribute any copyright protected works. This tutorial will only guide you through running the game using your own licensed copy of the game using legitimate CD keys
:::

At the core of LANCommander, games are distributed to clients as ZIP archives with media, metadata, and scripts prepared through the server's web interface. Upon installation, an authenticated LANCommander Launcher will extract the main game archive and execute any post-install scripts via its embedded PowerShell runtime.

To demonstrate how to configure a game from start to finish, this tutorial will cover how to configure the game *Call of Duty (2003)* as it requires almost all features of LANCommander for a proper installation.

## Creating the Game
:::info
Before continuing, it is strongly recommended that you link LANCommander to an IGDB account for automatic metadata retrieval. See the [settings documentation]() for more details.
:::

Within your LANCommander server's web interface, navigate to **Games** in the top navigation bar. Click the **Add Game** button at the top of the page.

A blank form will now appear. Enter the name **Call of Duty** into the *Title* field and click **Lookup**. A modal will appear with a list of games pulled from IGDB that may match based on the title entered. Select the entry for **Call of Duty, 10/29/2003, Infinity Ward** and click the **Select** button.

The **General** panel will now be populated with the metadata pulled directly from IGDB. Click the **Save** button at the top of the page and the rest of the game's configuration will now be available for editing.

### General
The "General" panel of the game editor contains most of the metadata fields for a game. These are fields that can be used in the client extension to sort and filter games as well as providing a richer display of information for the game. 

| Field                 | Description                                                                                          | Data Type    |
|-----------------------|------------------------------------------------------------------------------------------------------|--------------|
| Title                 | The display name of the game                                                                         | String       |
| Sort Title            | Optional title to change the sorting of games. Useful for games in a collection e.g. Call of Duty 1  | String       |
| Notes                 | Private notes for a game for admin use                                                               | String       |
| Description           | A description about the game                                                                         | String       |
| Engine                | The engine that a game is built upon                                                                 | Lookup       |
| Type                  | Represents if the game is standalone, mod, expansion, etc. See [Game Types]() for more information   | Select       |
| Base Game             | Only accessible in games that are not marked as "Main Game" in the Type field                        | Select       |
| Key Allocation Method | The method in which to allocate keys, e.g. user account or computer MAC address                      | Select       |
| Released On           | The release date for the game                                                                        | DateTime     |
| Singleplayer          | Denotes the game has a singleplayer mode                                                             | Checkbox     |
| Developers            | The list of developers that worked on the game                                                       | Tag List     |
| Publishers            | The list of publishers for the game                                                                  | Tag List     |
| Platforms             | The list of platforms the game is available on                                                       | Tag List     |
| Genres                | The list of genres for the game                                                                      | Tag List     |
| Tags                  | A list of tags for the game                                                                          | Tag List     |
| Collections           | The list of collections that the game belongs to                                                     | Tag List     |
| Redistributables      | A selectable list of redistributables that the game requires to be installed                         | Multi Select |

### Game Types
If you have a game that requires another game to be installed, you may have to specify the game type to modify the behavior of the installation.

|         Type          |                                                                                                            Description                                                                                                            |   |
|:---------------------:|:---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------:|---|
| Main Game             | The game requires no special treatment. It is not dependent on any  other game being installed. Most games will utilize this type.                                                                                                |   |
| Expansion             | This "game" is an expansion for another game. When starting a game,  the base game and expansion actions are selectable in Playnite. The  expansion's archive is extracted to the same directory as the base game.                |   |
| Standalone Expansion  | This game entry is displayed separate from the base game in the game  library. Installing a standalone expansion will initiate the install of  the base game. The archive files are extracted to the expansion's own  directory.  |   |
| Mod                   | The contents of the archive are extracted to the same location as  the base game and installed when the base game is installed. Actions are  merged and displayed on the client.                                                  |   |
| Standalone Mod        | The contents of the archive are extracted to the same location as  the base game, but the mod is presented as a separate game in the  library. Installing a standalone mod will trigger the install of the  base game.            |   |

### Actions
This is a table-based form for defining actions for a game. Actions are the entry points to your game. For *Call of Duty* we can specify two actions: singleplayer and multiplayer. The game has two separate executables (`CoDSP.exe` and `CoDMP.exe` respectively) for launching the game. By adding two separate actions, we can allow the user to choose either of these when launching the game from the launcher. Add two actions and populate them with the following information:

|             Name             |    Path    | Arguments  | Working Directory  | Primary  |
|:----------------------------:|:----------:|:----------:|:------------------:|:--------:|
| Call of Duty (Multiplayer)   | `CoDMP.exe`  |            | `{InstallDir}`       | True     |
| Call of Duty (Singleplayer)  | `CoDSP.exe`  |            | `{InstallDir}`       | True     |

As indicated by `{InstallDir}`, actions can make use of some variables. The following variables will be expanded to their appropriate value when starting an action:

|          Name          |                                                              Description                                                              |
|:----------------------:|:-------------------------------------------------------------------------------------------------------------------------------------:|
| `{DisplayWidth}`       | The width of the primary display in pixels                                                                                            |
| `{DisplayHeight}`      | The height of the primary display in pixels                                                                                           |
| `{DisplayRefreshRate}` | The refresh rate of the primary display in Hz                                                                                         |
| `{DisplayBitDepth}`    | The bit depth of the primary display in bits per pixel                                                                                |
| `{ServerAddress}`      | The address of the currently authenticated LANCommander server                                                                        |
| `{IPXRelayHost}`       | The host of the IPX relay as specified in Settings. If a hostname is  provided, an attempt to resolve to an IP address will be made.  |
| `{IPXRelayPort}`       | The port of the IPX relay as specified in Settings                                                                                    |

In addition to these variables, any [environment variables](https://ss64.com/nt/syntax-variables.html) (`%TEMP%`, `%AppData%`, etc.) and [special folders](https://learn.microsoft.com/en-us/dotnet/api/system.environment.specialfolder?view=net-8.0) (as `%MyDocuments%`, `%CommonPrograms%`, etc) will be expanded as well.

Servers may also define custom variables in order to provide direct connection details such as IP and port. For more information, see [Servers](/Server/Servers).

### Multiplayer
The multiplayer panel is used to denote the types of multiplayer available for the game. This is purely additional metadata. Defining these can make it extremely useful in LAN scenarios where you can find the right game for your session's player count. Enter the following modes for *Call of Duty*:
|  Type  | Min Player | Max Players | Protocol | Description |
|:------:|:----------:|:-----------:|:--------:|:-----------:|
| Online | 2          |  64         | TCP/IP   |             |
| LAN    | 2          |  64         | TCP/IP   |             |

### Saves
LANCommander has the ability to store save games similar to how cloud saves work in other platforms. These saves are compressed into a ZIP file and stored into the logged in user's profile on the server after the game has been closed. They will be restored on every game launch, if accessible. Save paths can point to either the registry or a file path on disk. Enter the following save paths for *Call of Duty*:
|   Type    |                         Path                          |
|:---------:|:-----------------------------------------------------:|
| Registry  | `HKLM:\SOFTWARE\WOW6432Node\Activision\Call of Duty`  |
| File      | `{InstallDir}/Main/config_mp.cfg `                    |

#### Using regex with saves

Some games, especially older games, would use combined directories to keep saves and other game files all together, using things like file extensions to differentiate the files. This is where it might be useful to use a regex for the file path rather than just providing a folder.

Let's say you want the files with the `.GM1` extension. For this, set the **Path** to `.*GM1$` and the folder to the path where saves are stored, eg `{InstallDir}/SomeGame`. It is recommended that you make the **Working Directory** as close to the location as possible where the files will be, to reduce potential false positives with things like the path matching the regex.

Also, make sure to check the **Regex** option on the save item.

### Keys
One of the biggest pains of setting up games in a LAN party (besides running through installers) is the management of CD keys. With this in mind, LANCommander has the ability to store and allocate keys without requiring the user to see or enter the key. The keys panel of the game editor is used to define and store a list of keys that will be available to your players. In the next section, Scripts, you will see an example of how we can populate the key using a Key Change Script.

:::info
For obvious reasons, we can't distribute actual CD keys in this tutorial. You will have to source your own!
:::

To add your own keys, click the **Edit** button and a new modal will appear. Enter each of your CD keys as a new line in the text editor, then hit the **OK** button. The table will now populate with all of your keys, though obscured from prying eyes as a password field. There are a few columns in this table that will be populated once a key is allocated to a player.

### Scripts
The scripts panel is where we can really dive into the nitty gritty of installing a game. Scripts should be used to configure anything about a game that may be required for it to run properly. In many cases games may require files in a specific directory, or a valid registry key in a specific location in order to run.

LANCommander utilizes PowerShell for its scripting engine and a full runtime is embedded into the launcher. While PowerShell is very... powerful... it may seem intimidating to some. Provided below are some sample scripts for *Call of Duty* to get you started. For more information on how scripts can be used in LANCommander, refer to the [Scripting Documentation](/documentation/scripting) for available cmdlets/variables or [Games](/games) to see a directory of user-submitted game configurations.

#### Install Script
This script sets the correct FOV based on the display's aspect ratio and sets the game's resolution to match the display's native resolution.
```powershell
$Display = Get-PrimaryDisplay
$FOV = 80

if (($Display.Width / $Display.Height) -eq (16 / 9)) {
    $FOV = 96.4183
} elseif (($Display.Width / $Display.Height) -eq (16 / 10)) {
    $FOV = 90.3951
}

# Base Game Resolution
Write-ReplaceContentInFile -Pattern '^seta r_customheight(.+)' -Substitution "seta r_customheight ""$($Display.Height)""" -FilePath "$InstallDirectory\main\config_mp.cfg"
Write-ReplaceContentInFile -Pattern '^seta r_customwidth(.+)' -Substitution "seta r_customwidth ""$($Display.Width)""" -FilePath "$InstallDirectory\main\config_mp.cfg"
Write-ReplaceContentInFile -Pattern '^seta r_mode(.+)' -Substitution "seta r_mode ""-1""" -FilePath "$InstallDirectory\main\config_mp.cfg"
Write-ReplaceContentInFile -Pattern '^seta r_customaspect(.+)' -Substitution "seta r_customaspect ""1.7""" -FilePath "$InstallDirectory\main\config_mp.cfg"
Write-ReplaceContentInFile -Pattern '^seta cg_fov(.+)' -Substitution "seta cg_fov ""$FOV""" -FilePath "$InstallDirectory\main\config_mp.cfg"
```

#### Uninstall Script
It's a good idea to clean up any extra files or registry entries upon uninstall of a game. For *Call of Duty*, we only need to remove a registry key.
```powershell
Remove-Item -Path "registry::\HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Activision\Call of Duty" -Force -Recurse
```

#### Name Change Script
This script simply changes the id Tech 3 variable "name" in the multiplayer config file. This will ensure that any of your players will be set to their correct player name upon joining a server. This name is pulled directly from their profile name/alias in LANCommander.
```powershell
Write-ReplaceContentInFile -Pattern '^seta name (.+)' -Substitution "seta name ""$NewPlayerAlias""" -FilePath "$InstallDirectory\Main\config_mp.cfg"
```

#### Key Change Script
The LANCommander launcher will automatically track the allocation of a key across clients. Key change scripts should be used to update the key on the player's system.
```powershell
# Non-destructively creates path in registry
New-Item -Path "registry::\HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Activision"
New-Item -Path "registry::\HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Activision\Call of Duty"

# Creates or updates a key in the registry
New-ItemProperty -Path "registry::\HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Activision\Call of Duty" -Name "codkey" -Value $codkey -Force
```

### Archives
The final, but most important, step of creating a game in LANCommander is uploading the archive for the game. You will need to make a ZIP archive from the game files from a *Call of Duty* installation. At this point in the process it is worth checking out the wonderful [PCGamingWiki](https://pcgamingwiki.com) for any patches or game fixes that might be needed for modern systems.

Once you have your ZIP archive, click on the Upload Archive button. A modal will pop up where you can specify the version of the archive, a changelog (if needed), and then you can select your archive. Once a valid file is chosen, click the Upload button and your archive will begin uploading to the server.

## Final Steps
This tutorial has walked you through how to take a game and add it to LANCommander. If you have done everything correctly, you should now be able to see the game listed in Playnite after a library sync.

This tutorial used a fairly basic example of the type of game that LANCommander was built to work for. Games can get complicated depending on their use of configs and registry entries. On this site we have a fairly extensive list of [games](/games) that you can use as reference. If you're adding a game that's not on our list, feel free to contribute!

Also feel free to check out any other [tutorials](/tutorials)! Over time this section will become more populated with useful tools, script development tips, and common practices used by game installers. 