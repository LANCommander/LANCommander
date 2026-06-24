---
title: Redistributables
---

# Overview
Redistributables are common runtimes or libraries that a game might require in order to run. A common list of redistributables may include:
- Microsoft DirectX
- Microsoft Visual C++
- Microsoft .NET Framework
- NVIDIA PhysX
- Java Runtime
- OpenAL

LANCommander supports the ability to host and install these redistributables for clients. Similar to games, redistributable installation relies on scripts and archives.

# Required Configuration
A basic redistributable will need two types of scripts:
- [Detect Install](/Scripting/Script Types/Detect Install)
- [Install](/Scripting/Script Types/Install)

For more information on variables and requirements, please review the documentation for both script types. It is important to note that both scripts are required, where the **Detect Install** script will be used to verify if the redistributable is already installed and the **Install** script is used to actually handle the installation.

# Archives
In order to send the setup files to the client, an archive must be uploaded to the redistributable. These should be ZIP files and should include any files that might be required for the installation to execute.

# Assigning to Games
Games can be assigned redistributables in two ways:

- When editing a game, use the **Redistributables** multiselect field to choose any applicable redistributable
- When editing a redistributable, you may use the **Games** multiselect field to choose any game that might require the redistributable to be installed

# Compatibility Shims
Redistributables can also serve as **compatibility shims** — tools that wrap game execution for cross-platform support. Examples include [WINE](https://www.winehq.org/), [umu-launcher](https://github.com/Open-Wine-Components/umu-launcher), and [LANCommander.Interposer](https://github.com/LANCommander/LANCommander.Interposer).

A redistributable becomes a compatibility shim when it has an **Option Schema** defined. The option schema is a YAML document that describes configurable options and how to wrap the game executable at launch time.

## Option Schema
The option schema is defined in the **Option Schema** field on the redistributable's General page. It uses YAML with PascalCase keys and supports the following structure:

```yaml
CommandTemplate: umu-run {exe} {args}
Options:
  Game:
    Description: Game identification
    Options:
      GAMEID:
        Type: string
        IsEnvironmentVariable: true
        Default: umu-default
        Description: Game ID for protonfixes lookup
  Proton:
    Description: Proton configuration
    Options:
      PROTONPATH:
        Type: string
        IsEnvironmentVariable: true
        Default: GE-Proton
        Description: Proton version or path
```

### CommandTemplate
Defines how the game executable is wrapped. Use `{exe}` and `{args}` as placeholders for the original executable path and arguments. When a command template is defined, the launcher rewrites the process start info before launching.

### Options
A dictionary of option definitions. Options can be nested to create logical groupings. Group nodes (those with only child `Options` and no `Type`) serve as organizational containers. Leaf nodes (those with a `Type`) are the actual configurable values.

Each option definition supports the following fields:

| Field                   | Type       | Description                                                                                        |
|-------------------------|------------|----------------------------------------------------------------------------------------------------|
| `Type`                  | `string`   | The data type: `string`, `bool`, `int`, `choice`, or `list`                                        |
| `Default`               | `string`   | The default value if none is configured. For `list`, a YAML sequence (see below).                  |
| `Description`           | `string`   | A human-readable description shown in the admin UI                                                 |
| `Required`              | `bool`     | Whether the option must be configured                                                              |
| `IsEnvironmentVariable` | `bool`     | If `true`, the resolved value is set as a process environment variable using the option's key name |
| `Choices`               | `string[]` | Available values for `choice` type options                                                         |
| `Options`               | `dict`     | Child options for creating nested groups                                                           |
| `ItemType`              | `string`   | For scalar `list` options: the type of each item (`string`, `int`, `bool`). Defaults to `string`.  |
| `Fields`                | `dict`     | For composite `list` options: per-row sub-schema. Presence of `Fields` makes the list composite.   |
| `MinItems` / `MaxItems` | `int`      | For `list` options: lower/upper bounds on the number of rows. Both optional.                       |

Nested options are flattened using dot-notation keys for storage and resolution (e.g., `Game.GAMEID`). When `IsEnvironmentVariable` is `true`, only the leaf key name is used as the environment variable name (e.g., `GAMEID`, not `Game.GAMEID`).

### List Options
Use `Type: list` when an option needs to hold a variable number of values — for example, repeated INI entries like `ListFactories[0]=…`, `ListFactories[1]=…`, etc.

A list option is either **scalar** (a list of plain values) or **composite** (a list of records). The shape of each row is determined entirely by the schema; how the values are emitted into the target config is up to the redistributable's scripts.

**Scalar list** — each row is a single value:
```yaml
Options:
  AllowedHosts:
    Type: list
    ItemType: string
    Default:
      - localhost
      - example.com
```

**Composite list** — each row is a record with named fields. A list-level `Default:` seeds initial rows; per-field `Default:` inside `Fields` is used to prefill new rows when an admin clicks "+ Add Item":
```yaml
Options:
  MasterServers:
    Type: list
    DisplayName: Master Servers
    Fields:
      Address:
        Type: string
        Default: master.example.com
      Port:
        Type: int
        Default: 28900
      GameName:
        Type: string
        Default: unreal
    MinItems: 1
    MaxItems: 16
    Default:
      - { Address: master.oldunreal.com, Port: 28900, GameName: unreal }
      - { Address: master.hlkclan.net,   Port: 28900, GameName: unreal }
```

**Consuming list values in scripts.** `Get-RedistributableOptions` hydrates list values into native PowerShell arrays — scalar lists become typed arrays (`string[]`/`int[]`/`bool[]`), composite lists become arrays of `PSObject`s keyed by `Fields`. The script decides the on-disk format:
```powershell
$opts = Get-RedistributableOptions -Path $InstallDirectory -Id $GameId -Name "UBrowser"
$lines = @()
for ($i = 0; $i -lt $opts.MasterServers.Count; $i++) {
    $s = $opts.MasterServers[$i]
    $lines += "ListFactories[$i]=UBrowser.UBrowserGSpyFact,MasterServerAddress=$($s.Address),MasterServerTCPPort=$($s.Port),GameName=$($s.GameName)"
}
$lines | Set-Content (Join-Path $InstallDirectory "System/UnrealTournament.ini")
```

**Environment variables.** `IsEnvironmentVariable: true` is ignored for `Type: list` because environment variables are scalar. Read list values from `Get-RedistributableOptions` in a script instead.

## Per-Game Options
When a game is assigned a redistributable that has an option schema, the game's **Redistributables** page will display form fields for each option. Administrators can configure values specific to that game (e.g., setting the correct `GAMEID` for protonfixes). These values are stored on the game-redistributable relationship and override the schema defaults.

## Resolution Order
Option values are resolved in the following order, with later values taking precedence:

1. **Schema defaults** — the `Default` value defined in the option schema
2. **Per-game values** — configured by the admin on the game's Redistributables page

## Run Wrapper Scripts
For compatibility tools that require more complex execution logic than a simple command template (e.g., DLL injection), redistributables can define a [Run Wrapper](/Scripting/Script Types/Run Wrapper) script. This script receives the executable path, arguments, working directory, and all resolved option values, and is responsible for launching the process.

## Accessing Options in Scripts
Options can be accessed within any game script using the `Get-RedistributableOptions` cmdlet. See the [Cmdlets](/Scripting/Cmdlets) documentation for details.

# Install Process
When a game is installed via the [SDK](/SDK/Overview) or [launcher](/Launcher/Overview), it includes a list of redistributables that have been assigned. For each of these redistributables, the client will execute the [Detect Install](/Scripting/Script Types/Detect Install) script. If the script has determined that there is no prior installation, the client will then download the archive and extract it to the user's temp directory. It will then execute the [Install](/Scripting/Script Types/Install) script with the working directory set to the destination of the archive's extraction.