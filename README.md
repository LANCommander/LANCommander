# LANCommander

![GitHub Release](https://img.shields.io/github/v/release/LANCommander/LANCommander)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/LANCommander/LANCommander)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/LANCommander/LANCommander/LANCommander.Release.yml?branch=main)
![GitHub License](https://img.shields.io/github/license/LANCommander/LANCommander)
![Documentation](https://img.shields.io/website?url=https%3A%2F%2Fdocs.lancommander.app&label=documentation)
![Discord](https://img.shields.io/discord/1134004697712316506)
[![Support me on Patreon](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fshieldsio-patreon.vercel.app%2Fapi%3Fusername%3DLANCommander%26type%3Dpatrons&style=flat)](https://patreon.com/LANCommander)

**Self-hosted game distribution - no internet required.**

LANCommander is an open-source, self-hostable digital game platform. Host your own game library, distribute games across your local network, and let players browse and install from a polished desktop launcher. Originally built for LAN parties where internet access isn't available, but works anywhere you want a private game distribution platform.

## Screenshots

<table>
  <tr>
    <td><img src="LANCommander.Documentation/Releases/_Assets/2.1.0 - Depot.jpg" alt="Game Depot - browse popular games, genres, and new releases" /></td>
    <td><img src="LANCommander.Documentation/Releases/_Assets/2.1.0 - Launcher.jpg" alt="Game Detail - view game info, media, and launch games" /></td>
  </tr>
  <tr>
    <td><em>Depot - browse by genre, popularity, and new releases</em></td>
    <td><em>Game detail view with media, description, and play controls</em></td>
  </tr>
  <tr>
    <td><img src="LANCommander.Documentation/Releases/_Assets/2.1.0 - Shelf.jpg" alt="Game Library - your full collection at a glance" /></td>
    <td><img src="LANCommander.Documentation/Releases/_Assets/2.1.0 - Server - Preview.png" alt="Server Admin - web-based game management dashboard" /></td>
  </tr>
  <tr>
    <td><em>Library shelf - your full game collection at a glance</em></td>
    <td><em>Server admin dashboard - manage games, users, and settings</em></td>
  </tr>
</table>

## Features

### Game Distribution
- **Self-hosted game library** - host and distribute games over your local network
- **No internet dependency** - all assets served locally, perfect for closed networks
- **Multi-part archive uploads** with chunked transfer for large games
- **Automatic metadata lookup** from IGDB and SteamGridDB (covers, icons, backgrounds)
- **Redistributable management** - bundle and auto-install Visual C++, .NET, DirectX, etc.

### Desktop Launcher
- **Polished game browser** with depot storefront, library shelf, and search/filtering
- **Download queue** with progress tracking and install management
- **Cloud saves** - automatic backup and restore of game save files
- **Auto-discovery** - finds servers on your network via UDP beacon
- **Media viewer** - browse screenshots and video trailers for each game
- **Discord Rich Presence** integration
- **Offline mode** support

### Server Administration
- **Web-based dashboard** built on ASP.NET Blazor
- **User management** with registration, approval workflows, and role-based access
- **OpenID Connect** authentication support
- **PowerShell scripting** - pre/post install, pre/post launch hooks with a built-in editor
- **Dedicated server management** with RCON support
- **Play session tracking** and statistics

### Game Packaging
- **Dedicated Packager tool** with step-by-step wizard for creating game packages
- **Automatic metadata lookup** - search IGDB and auto-fill game details
- **Registry capture** and installer monitoring
- **Direct upload** to your LANCommander server

### Developer SDK
- **Published on NuGet** - [`LANCommander.SDK`](https://www.nuget.org/packages/LANCommander.SDK)
- Build custom clients and tools with the .NET SDK
- Full API access via dependency injection

### Platform Support
| Component | Windows | Linux | macOS |
|-----------|:-------:|:-----:|:-----:|
| Server    | x64, arm64 | x64, arm64 | x64, arm64 |
| Launcher  | x64, arm64 | x64, arm64 | x64, arm64 |
| Packager  | x86     | -     | -     |

> **Note:** The launcher is primarily tested on Windows. Linux and macOS builds are provided but may have limited functionality.

## Quick Start

### Docker (Recommended)

A Docker image is available on [Docker Hub](https://hub.docker.com/r/lancommander/lancommander):

```yaml
services:
  lancommander:
    image: lancommander/lancommander:latest
    container_name: lancommander
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Etc/UTC
      # Uncomment the line below to install SteamCMD
      # - STEAMCMD=1
      # Uncomment the line below to install WINE
      # - WINE=1
    volumes:
      - /path/to/app/data:/app/Data
    ports:
      - 1337:1337/tcp   # Web interface
      - 35891:35891/udp # Beacon broadcast
      - 213:213/udp     # IPX relay
    restart: unless-stopped
```

All config and uploaded game archives are stored in `/app/Data`.

### Standalone

Download the latest release for your platform from the [Releases](https://github.com/LANCommander/LANCommander/releases) page. The server is a self-contained binary with no external dependencies beyond a supported OS.

### SteamCMD Support

The Docker image supports optional SteamCMD installation. Set `STEAMCMD=1` to enable it. SteamCMD will be installed in `/app/Data/Steam` with cached credentials persisted across container restarts.

### WINE Support

Set `WINE=1` to install WINE with wine32, wine64, and winetricks support in the Docker container.

> _Note: The Docker image runs the Linux build. Features such as server management may be limited._

## FAQ

### How do I get games?
LANCommander is a management and distribution platform; it does not come bundled with any games. The best candidates are portable, DRM-free, freeware, shareware, or abandonware titles. Join our [Discord](https://discord.gg/vDEEWVt8EM) where the community shares pre-packaged freeware games.

### I have a large LAN party planned. What about scaling?
LANCommander communicates over HTTP(S). All downloads are served through standard API routes (`/api/Games/{id}/Download`), making it compatible with reverse proxies and LAN caching solutions.

### Where can I get help?
- **Documentation:** [lancommander.app](https://lancommander.app/)
- **Community:** [Discord](https://discord.gg/vDEEWVt8EM) - the best place for help with the platform, scripting, and game packaging

### How do I contribute?
See [CONTRIBUTING.md](CONTRIBUTING.md) for build instructions and contribution guidelines. If you're not a developer, contributing to the [documentation](https://lancommander.app/) is a great way to help.

## Roadmap

The following features are being considered for future releases:
- Peer-to-peer file sharing of game files
- Built-in VPN client/server for remote LAN parties
- Integration with platforms such as Discord, TeamSpeak, Mumble

## Community

- [Discord](https://discord.gg/vDEEWVt8EM) - Discussion, support, and game sharing
- [Documentation](https://lancommander.app/) - Guides and reference

## License

LANCommander is licensed under the [MIT License](LICENSE).
