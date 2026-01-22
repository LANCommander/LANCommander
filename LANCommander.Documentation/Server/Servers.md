---
title: Servers
---

# Overview
In addition to the installation of games, LANCommander can manage dedicated game servers that run on the same machine as the server application. With a tight integration between the server and game execution, this unlocks some additional features not seen in other platforms and can ease the pain of administration.

## Adding a Server
By going to **Servers** under the main navigation, a new server can be configured using the **Add Server** button. Servers can also be imported from a `.LCX` file.

## General
Under this section, you can provide the following information:
|        Field       |                                                                             Description                                                                              |
|:------------------:|:--------------------------------------------------------------------------------------------------------------------------------------------------------------------:|
| Name               | The name for the server                                                                                                                                              |
| Game               | The game that should be associated with the server                                                                                                                   |
| Executable Path    | The main path for the server's executable                                                                                                                            |
| Arguments          | Arguments that will be passed to the main executable                                                                                                                 |
| Working Directory  | The location in which the main executable will be called from                                                                                                        |
| Host               | The hostname to use for any connection details passed to games (defaults to server IP)                                                                               |
| Port               | The port to use for any connection details passed to games                                                                                                           |
| Use Shell Execute  | This option specifies whether you would like to run the server using the shell. Some servers may require this as they will have a UI or won't output logs to stdout. |
| Termination Method | The termination method used to kill the server on stop                                                                                                               |
## Actions
If your server is tied to a game, you can also specify custom actions. These actions will be treated like any other game action and will be displayed in the launcher.

This can be extremely useful to pass connection details to games that support direct connection via command line arguments. For example, we can specify a direct connection action for the game *Battlefield Vietnam* by supplying the following information:
|       Field       |                   Description                    |
|:-----------------:|:------------------------------------------------:|
| Name              | Join Multiplayer Server                          |
| Path              | `{InstallDir}\BfVietnam.exe`                       |
| Arguments         | `+restart 1 +joinServer {ServerHost}:{ServerPort}` |
| Working Directory | `{InstallDir}`                                     |
| Primary           | Checked                                          |

## Autostart
Servers can be set to autostart based on two methods:
- **On Application Start**
The server will be started after the LANCommander server has started. Useful for any servers that want to be always kept running.
- **On Player Activity**
The server will be started as soon as the first player starts the game. The server will also be killed when the last player stops the game.

A delay in seconds can also be specified. This may be useful if you have multiple servers starting on application start and would like to stagger their initialization.

## HTTP
HTTP paths are a way to host static files such as maps and other assets. Engines such as Source, id Tech 3, and Unreal can utilize HTTP for faster downloads when connecting to a server.

For example, you may have a server for *Counter-Strike: Source* that needs to host FastDL downloads. You could enter the following paths:
|                     Local Path                    |   Path   |
|:-------------------------------------------------:|:--------:|
| `C:\LANCommander\Servers\Steam\css\cstrike\maps`  | `/maps`  |
| `C:\LANCommander\Servers\Steam\css\cstrike\sound` | `/sound` |

This will enable two basic HTTP endpoint visible by clicking the eye icon. e.g.:
- `http://lancommander:1337/Server/8ccc3778-fb80-405a-8643-54ea9b2f58b4/maps`
- `http://lancommander:1337/Server/8ccc3778-fb80-405a-8643-54ea9b2f58b4/sound`

Now you can use these paths in your server config to have FastDL compatible HTTP endpoints.

## Consoles
:::warning
This feature is incomplete and will be refactored in future releases.
:::

## Scripts
Servers can also specify PowerShell scripts that can execute on stop and after start of the server. This may be useful if any advanced configuration is needed upon runtime.