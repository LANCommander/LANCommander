.---
title: Cmdlets
---

# Overview
Since there is a full PowerShell runtime built into LANCommander, there are a few custom cmdlets that have been added to simplify common tasks that may be needed when installing or configuring a game. This page covers the definition and use of these cmdlets.

## `Convert-AspectRatio`
Calculates a resolution for the desired aspect ratio using an input width and height in pixels.

### Syntax
```powershell
Convert-AspectRatio
    -Width <int>
    -Height <int>
    -AspectRatio <double>
```

### Description
The `Convert-AspectRatio` cmdlet is most useful for calculating a resolution for a specific aspect ratio that will fit within a display by either using pillar or letter boxing. For example, some games may only support 4:3 displays and you may want to calculate the correct 4:3 resolution from your 16:9 display. This cmdlet is really useful when paired with `Get-PrimaryDisplay`. 

### Example
```powershell
Convert-AspectRatio -Width 2560 -Height 1440 -AspectRatio (4 / 3)

# Returns <DisplayResolution>
Width     : 1920
Height    : 1440
```

## `ConvertTo-StringBytes`
Converts an input string into a byte array.

### Syntax
```powershell
ConvertTo-StringBytes
    -Input <string>
    -Utf16 <bool>
    -BigEndian <bool>
    -MaxLength <int>
    -MinLength <int>
```

### Description
`ConvertTo-StringBytes` is extremely useful for patching strings in binary files. It will take any input string and convert it to a byte array. Length can be controlled using the `-MaxLength` and `-MinLength` parameters. Endianness can be set using `-BigEndian`. If the string must be UTF-16 (easily identifiable as characters separated by `0x00`), use `-Utf16`.

### Example
```powershell
ConvertTo-StringBytes -Input "Hello, world!" -Utf16 1
72 0 101 0 108 0 108 0 111 0 44 0 32 0 119 0 111 0 114 0 108 0 100 0 33 0

ConvertTo-StringBytes -Input "Hello, world!" -MaxLength
72 101 108 108 111

ConvertTo-StringBytes -Input "Hello" -MaxLength 10 -MinLength 10
72 101 108 108 111 0 0 0 0 0

ConvertTo-StringBytes -Input "Hello" -Utf16 1 -BigEndian 1
0 72 0 101 0 108 0 108 0 111
```

## `Edit-PatchBinary`
Patches binary files at a specified offset.

### Syntax
```powershell
Edit-PatchBinary
    -Offset <long>
    -Data <byte[]>
    -FilePath <string>
    -MaxLength <int>
    -MinLength <int>
```

### Description
This cmdlet is useful when a binary file has to be patched at a specific offset. It can be extremely useful when paired with `ConvertTo-StringBytes` to update a player name in a binary file.

### Example
```powershell
$bytes = ConvertTo-StringBytes -Input "Master Chief" -Utf16 1 -MaxLength 16 -MinLength 16

Edit-PatchBinary -FilePath "$($env:LOCALAPPDATA)\Microsoft\Halo 2\Saved Games\S0000000\profile" -Offset 0x08 -Data $bytes
```

## `Get-GameManifest`
Parses a game's manifest YAML file from the specified install directory.

### Syntax
```powershell
Get-GameManifest
    -Path <string>
```

### Description
Used to deserialize a game's manifest file (`Manifest.yml`) from the specified install directory. Returns the game manifest as an object.

### Examples
```powershell
$manifest = Get-GameManifest -Path "C:\Games\Age of Empires II - The Age of Kings"
Write-Host $manifest.Title

Age of Empires II: The Age of Kings
```

## `Get-PrimaryDisplay`
Gets the bounds of the machine's current primary display.

### Syntax
```powershell
Get-PrimaryDisplay
```

### Description
The `Get-PrimaryDisplay` cmdlet takes no parameters and will only return the bounds of the current primary display attached to the machine. This is highly useful in where you might want to automatically set the game's resolution to match the primary display's resolution.

### Example
```powershell
$Display = Get-PrimaryDisplay

Write-Host "$($Display.Width)x$($Display.Height) @ $($Display.RefreshRate)Hz"

1920x1080 @ 120Hz
```

## `Update-IniValue`
Updates the value of an INI file.

### Syntax
```powershell
Update-IniValue
    -Section <string>
    -Key <string>
    -Value <string>
    -FilePath <string>
    -WrapValueInQuotes <bool> (optional)
```

### Description
`Update-IniValue` should be used when updating the values of an INI file. These files are typically used for configuring games and may be hard to edit using `Write-ReplaceContentInFile` and regular expressions. INI files are comprised of sections (text surrounded in square brackets, (`[Display]`), and key-value pairs (`Width=1024`).

### Example
```powershell
# Change the resolution
$Display = Get-PrimaryDisplay
Update-IniValue -Section "Display" -Key "Width" -Value "$($Display.Width)" -FilePath "$InstallDirectory\config.ini"
```

## `Write-GameManifest`
Serializes a `GameManifest` object and writes it to disk.

### Syntax
```powershell
Write-GameManifest
    -Path <string>
    -Manifest <LANCommander.SDK.GameManifest>
```

### Example
```powershell
$manifest = Get-GameManifest -Path "C:\Games\Age of Empires II - The Age of Kings"
$manifest.SortTitle = "Age of Empires 2"

Write-GameManifest -Path "C:\Games\Age of Empires II - The Age of Kings\.lancommander\$($manifest.Id)\Manifest.yml"
```

## `Write-ReplaceContentInFile`
Find and replace a string in a text file.

### Syntax
```powershell
Write-ReplaceContentInFile
    -Pattern <string>
    -Substitution <string>
    -FilePath <string>
```

### Description
`Write-ReplaceContentInFile` can be used when you want to edit a text file and replace content. The `-Pattern` parameter accepts regular expressions.

### Example
```powershell
# Changes the player's multiplayer name in Call of Duty (2003)
Write-ReplaceContentInFile -Pattern '^seta name (.+)' -Substitution "seta name ""$NewPlayerAlias""" -FilePath "$InstallDirectory\Main\config_mp.cfg"
```

## `Get-UserCustomField`
Retrieves the value of a custom field from the user's profile from the server.

### Syntax
```powershell
Get-UserCustomField
    -Name <string>
```

### Description
This cmdlet can be useful if you have a game that might require a persistent ID attached to your user. Often times games will assign a unique ID to a player upon creation of a profile, and that ID will be used on a server to keep track of stats, inventory, etc. The list of custom fields added to a user can be viewed under their profile in the server's web UI.

### Example
```powershell
Get-UserCustomField -Name "SteamId"
```

## `Update-UserCustomField`
Updates the value of a custom field on a users profile.

### Syntax
```powershell
Update-UserCustomField
    -Name <string>
    -Value <string>
```

### Description
The companion to `Get-UserCustomField`, this cmdlet lets you update or set the value of a custom field on a user's profile directly within your scripts. The most common use case is generating a new user ID on install, writing it to the game's configuration file, and then updating the custom field on the user's profile to store it for subsequent installs.

### Example
```powershell
Update-UserCustomField -Name "SteamId" -Value "34950494"
```

# Steam-Related Cmdlets

The following cmdlets provide functionality for interacting with SteamCMD and the Steam Store API. These cmdlets enable you to install Steam games, manage SteamCMD profiles, search for games, and retrieve Steam assets.

# Connection Management

## `Connect-SteamCmd`
Connects to SteamCMD with the specified username and optional password.

### Syntax
```powershell
Connect-SteamCmd
    -Username <string>
    -Password <SecureString> (optional)
```

### Description
The `Connect-SteamCmd` cmdlet authenticates with SteamCMD using the provided username and optional password. This is required before installing Steam content that requires authentication. Returns a `SteamCmdStatus` object indicating the connection result.

### Example
```powershell
$securePassword = ConvertTo-SecureString "mypassword" -AsPlainText -Force
Connect-SteamCmd -Username "myusername" -Password $securePassword
```

## `Disconnect-SteamCmd`
Disconnects from SteamCMD for the specified username.

### Syntax
```powershell
Disconnect-SteamCmd
    -Username <string>
```

### Description
The `Disconnect-SteamCmd` cmdlet logs out the specified username from SteamCMD. Returns a `SteamCmdStatus` object indicating the logout result.

### Example
```powershell
Disconnect-SteamCmd -Username "myusername"
```

## `Get-SteamCmdConnectionStatus`
Gets the connection status for a SteamCMD username.

### Syntax
```powershell
Get-SteamCmdConnectionStatus
    -Username <string>
```

### Description
The `Get-SteamCmdConnectionStatus` cmdlet retrieves the current connection status for the specified username. Returns a `SteamCmdConnectionStatus` object containing information about whether the user is connected and authenticated.

### Example
```powershell
$status = Get-SteamCmdConnectionStatus -Username "myusername"
Write-Host "Connected: $($status.IsConnected)"
```

# SteamCMD Configuration

## `Get-SteamCmdPath`
Gets the path to the SteamCMD executable.

### Syntax
```powershell
Get-SteamCmdPath
```

### Description
The `Get-SteamCmdPath` cmdlet attempts to auto-detect the SteamCMD executable path on the system. Returns the path as a string if found, or nothing if SteamCMD is not detected.

### Example
```powershell
$steamCmdPath = Get-SteamCmdPath
if ($steamCmdPath) {
    Write-Host "SteamCMD found at: $steamCmdPath"
}
```

## `Get-SteamCmdProfile`
Gets a SteamCMD profile for the specified username.

### Syntax
```powershell
Get-SteamCmdProfile
    -Username <string>
```

### Description
The `Get-SteamCmdProfile` cmdlet retrieves the SteamCMD profile configuration for the specified username. Returns a `SteamCmdProfile` object containing the username and install directory, or nothing if the profile doesn't exist.

### Example
```powershell
$profile = Get-SteamCmdProfile -Username "myusername"
if ($profile) {
    Write-Host "Install Directory: $($profile.InstallDirectory)"
}
```

## `Get-SteamCmdProfiles`
Gets all SteamCMD profiles.

### Syntax
```powershell
Get-SteamCmdProfiles
```

### Description
The `Get-SteamCmdProfiles` cmdlet retrieves all configured SteamCMD profiles. Returns a collection of `SteamCmdProfile` objects.

### Example
```powershell
$profiles = Get-SteamCmdProfiles
foreach ($profile in $profiles) {
    Write-Host "$($profile.Username): $($profile.InstallDirectory)"
}
```

## `Set-SteamCmdProfile`
Creates or updates a SteamCMD profile.

### Syntax
```powershell
Set-SteamCmdProfile
    -Username <string>
    -InstallDirectory <string>
```

### Description
The `Set-SteamCmdProfile` cmdlet creates or updates a SteamCMD profile with the specified username and install directory. This profile is used to store SteamCMD configuration settings.

### Example
```powershell
Set-SteamCmdProfile -Username "myusername" -InstallDirectory "C:\Steam\Content"
```

## `Remove-SteamCmdProfile`
Removes a SteamCMD profile.

### Syntax
```powershell
Remove-SteamCmdProfile
    -Username <string>
```

### Description
The `Remove-SteamCmdProfile` cmdlet deletes the SteamCMD profile for the specified username.

### Example
```powershell
Remove-SteamCmdProfile -Username "myusername"
```

# Steam Content Installation

## `Install-SteamContent`
Installs Steam content (game, DLC, etc.) using SteamCMD.

### Syntax
```powershell
Install-SteamContent
    -AppId <uint>
    -InstallDirectory <string>
    -Username <string> (optional)
```

### Description
The `Install-SteamContent` cmdlet queues an installation job to download and install Steam content using SteamCMD. The `AppId` parameter specifies the Steam App ID to install, and `InstallDirectory` is where the content will be installed. If `Username` is provided, it will use that profile's authentication. Returns a `SteamCmdInstallJob` object that can be used to track the installation progress.

### Example
```powershell
$job = Install-SteamContent -AppId 730 -InstallDirectory "C:\Games\Counter-Strike 2" -Username "myusername"
Write-Host "Installation job started: $($job.Id)"
```

## `Remove-SteamContent`
Removes Steam content from the specified install directory.

### Syntax
```powershell
Remove-SteamContent
    -InstallDirectory <string>
```

### Description
The `Remove-SteamContent` cmdlet removes Steam content from the specified installation directory. Returns a `SteamCmdStatus` object indicating the result of the operation.

### Example
```powershell
Remove-SteamContent -InstallDirectory "C:\Games\Counter-Strike 2"
```

# Steam Store

## `Search-SteamGames`
Searches for games on the Steam Store.

### Syntax
```powershell
Search-SteamGames
    -Keyword <string>
```

### Description
The `Search-SteamGames` cmdlet searches the Steam Store for games matching the specified keyword. Returns a collection of `GameSearchResult` objects containing the game name and App ID.

### Example
```powershell
$results = Search-SteamGames -Keyword "Counter-Strike"
foreach ($result in $results) {
    Write-Host "$($result.Name) - App ID: $($result.AppId)"
}
```

## `Get-SteamWebAssetUri`
Gets the URI for a Steam web asset (logo, header, etc.).

### Syntax
```powershell
Get-SteamWebAssetUri
    -AppId <int>
    -WebAssetType <WebAssetType>
```

### Description
The `Get-SteamWebAssetUri` cmdlet returns the URI for a specific web asset type for the given Steam App ID. The `WebAssetType` parameter accepts one of the following values:
- `Capsule` - Small capsule image (231x87)
- `CapsuleLarge` - Large capsule image (616x353)
- `Header` - Header image
- `HeroCapsule` - Hero capsule image
- `LibraryCover` - Library cover image (600x900)
- `LibraryHeader` - Library header image
- `LibraryHero` - Library hero image
- `Logo` - Game logo (PNG)

Returns a `Uri` object.

### Example
```powershell
$logoUri = Get-SteamWebAssetUri -AppId 730 -WebAssetType Logo
Write-Host "Logo URL: $logoUri"
```

## `Get-SteamAppInfo`
Gets app details from the Steam Store (no API key required) and the **changenumber** (build ID) and last updated time from Steam via SteamKit2 (PICS).

### Syntax
```powershell
Get-SteamAppInfo
    -AppId <uint>
```

### Description
The `Get-SteamAppInfo` cmdlet returns a `SteamAppInfo` object with name, short description, release date, developers, publishers, and store URL from the public Steam Store `appdetails` endpoint. It also connects to Steam via SteamKit2 (PICS) to fill **LastChangenumber** and **LastUpdated** for the public branch—no Web API key required. If SteamKit2 cannot connect or the app cannot be queried, those two properties are null.

### Example
```powershell
$info = Get-SteamAppInfo -AppId 413150
if ($info) {
    Write-Host "Name: $($info.Name)"
    Write-Host "Description: $($info.Description)"
    Write-Host "Release date: $($info.ReleaseDate)"
    Write-Host "Developer: $($info.Developer)"
}
```