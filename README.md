# LANCommander

![GitHub Release](https://img.shields.io/github/v/release/LANCommander/LANCommander)
![GitHub commit activity](https://img.shields.io/github/commit-activity/m/LANCommander/LANCommander)
![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/LANCommander/LANCommander/LANCommander.Release.yml?branch=main)
![GitHub License](https://img.shields.io/github/license/LANCommander/LANCommander)
![Documentation](https://img.shields.io/website?url=https%3A%2F%2Fdocs.lancommander.app&label=documentation)
![Discord](https://img.shields.io/discord/1134004697712316506)
[![Support me on Patreon](https://img.shields.io/endpoint.svg?url=https%3A%2F%2Fshieldsio-patreon.vercel.app%2Fapi%3Fusername%3DLANCommander%26type%3Dpatrons&style=flat)](https://patreon.com/LANCommander)

LANCommander is an open-source digital game platform.

The main application is self-hostable and is built on the ASP.NET Blazor web application framework. Your self-hosted platform can be accessed through the offical LANCommander launcher. The database is implemented using SQLite so there is no complex setup required.

The platform is designed to work on local networks and loads no assets from the internet. It was originally developed to help assist a LAN party where the local network is closed and no internet access is permitted.

Builds for Windows, Linux, and macOS are provided, with varying levels of support/compatibility. **Currently the launcher has only been tested to run on Windows**

## Community
* [Discord](https://discord.gg/vDEEWVt8EM)
* [Patreon](https://patreon.com/LANCommander)

## Docker
A Docker image is available over at [Docker Hub](https://hub.docker.com/r/lancommander/lancommander). A sample compose file is provided below:

```yaml
services:
  lancommander:
    image: lancommander/lancommander:latest
    container_name: lancommander
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Etc/UTC
    volumes:
      - /path/to/appdata/config:/config
    ports:
      - 1337:1337/tcp   # Webinterface
      - 35891:35891/udp # Beacon Broadcast
    restart: unless-stopped
```

All config files are available from `/config`. This include any archive uploads for games. Many of these paths can be changed under Settings if you wish to add additional volume mappings.

_Note: The Docker image runs the Linux build and features such as server management may be limited._

## FAQ
### How do I get games?
The best games are either portable games or DRM-free games. Freeware, shareware, abandonware are all great available options. LANCommander is only a management/distribution system. It does not come bundled with any games.

It's worth joining our [Discord](https://discord.gg/vDEEWVt8EM) as some pre-packaged freeware games are available to download.

### I have a pretty large LAN party planned with hundreds of players. I have some sick infrastructure and a LAN cache. What do?
LANCommander communicates over HTTP(S). There is no LAN cache configuration provided, but all downloads are provided through the `/api/Games/{id}/Download` route.

### Where can I get some help?
Documentation can be found at our [documentation site](https://docs.lancommander.app/). The [Getting Started](https://docs.lancommander.app/en/GettingStarted) guide contains enough information to get your server up and running in minutes. 

Our soon to be retired [wiki](https://lancommander.app/index.php/Main_Page) is still available to plug any documentation holes while we work on updating the documentation site. [Tutorials](https://lancommander.app/index.php/Category:Tutorials) as well as sample configurations for [Games](https://lancommander.app/index.php/Category:Games) and [Redistributables](https://lancommander.app/index.php/Category:Redistributables) can be found there.

### How do I contribute?
Hit that fork button, submit a PR, there are no hard rules right now.

If you're not a developer but still want to contribute, contributing to our [documentation site](https://docs.lancommander.app/) is a great way to give back to the community!

The LANCommander dev team is currently spearheaded by one developer in their free time. Paid donation tiers are available over at the [Patreon](https://patreon.com/LANCommander) page.

## SDK
A separate assembly called `LANCommander.SDK` has been created for use in client applications. Here is a quick example of how one can authenticate to a LANCommander server and install a game to `C:\Games`:

```csharp
var client = new LANCommander.SDK.Client("http://localhost:1337", "C:\\Games");

await client.AuthenticateAsync("username", "password");

client.Games.Install("114f653d-ea91-484b-8fe9-8e9bb58bde81");
```

## To Do
LANCommander is far from complete. The basic implementation that exists will allow you to:
 - Manage games
 - Upload archives
 - Manage scripts
 - Manage keys
 - Download games
 - Basic user management
 - New user registration
 - Local "cloud" user saves
 - Dedicated server management/administration
 - Redistributable management and distribution
 - IPX Beacon for emulators such as DosBox
 - Game media management and automatic lookup (covers, icons, backgrounds)
 - Dedicated launcher for easy client setup

The following features are being considered:
 - Peer-to-peer file sharing of game files
 - Built-in VPN client/server for remote LAN parties
 - Social features including chat, friends list, etc.
 - Integration with platforms such as Discord, TeamSpeak, Mumble
