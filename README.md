
# LANCommander
LANCommander is a digital video game distribution system designed for LAN parties. 

The main application is self-hostable and is built on the ASP.NET Blazor web application framework. Instead of reinventing the wheel with yet-another-game-launcher, client-functionality has been implemented via a Playnite extension. The database is implemented using SQLite so there is no complex setup required.

The platform is designed to work on local networks and loads no assets from the internet. It was originally developed to help assist a LAN party where the local network is closed and no internet access is permitted.

Currently only Windows is supported. This may change in the future and a Docker container may be made available for the main web application.

## Community
* [Discord](https://discord.gg/vDEEWVt8EM)
* [Wiki](https://lancommander.app/index.php/Main_Page)

## FAQ
### How do I get games?
Do you have a peg leg and a parrot? There is no DRM implementation in LANCommander. The best games are either portable games or DRM-free games. Freeware, shareware, abandonware are all great available options. LANCommander is just a management/distribution system. It does not come bundled with any games.

### I have a pretty large LAN party planned with hundreds of players. I have some sick infrastructure and a LAN cache. What do?
LANCommander communicates over HTTP(S). There is no LAN cache configuration provided, but all downloads are provided through the `/api/Games/{id}/Download` route.

### How do I contribute?
Hit that fork button, submit a PR, there are no hard rules right now.

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
 - Game patching

The following features are being considered:

 - Dedicated server management/administration
 - Linux build
 - Some expansion of the dashboard with useful stats
 - Built-in VPN client/server for remote LAN parties
