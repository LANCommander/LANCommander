
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
The best games are either portable games or DRM-free games. Freeware, shareware, abandonware are all great available options. LANCommander is only a management/distribution system. It does not come bundled with any games.

### I have a pretty large LAN party planned with hundreds of players. I have some sick infrastructure and a LAN cache. What do?
LANCommander communicates over HTTP(S). There is no LAN cache configuration provided, but all downloads are provided through the `/api/Games/{id}/Download` route.

### Where can I get some help?
Some documentation lives at the [Wiki](https://lancommander.app/index.php/Main_Page) including a [Getting Started](https://lancommander.app/index.php/Tutorials:Getting_Started) guide and a category for [Tutorials](https://lancommander.app/index.php/Category:Tutorials). It also contains a large library of sample configurations for [Games](https://lancommander.app/index.php/Category:Games) and [Redistributables](https://lancommander.app/index.php/Category:Redistributables).

### How do I contribute?
Hit that fork button, submit a PR, there are no hard rules right now.

If you're not a developer but still want to contribute, writing documentation in the wiki is a great way to give back to the community!

The LANCommander dev team is currently spearheaded by one developer in their free time. If you feel compelled, [donations](https://www.paypal.com/donate/?business=LBJW6PFMFLULA&no_recurring=0&currency_code=USD) are always appreciated.

## SDK
A separate assembly called `LANCommander.SDK` has been created for use in client applications. The offical Playnite add-on utilizes this assembly to handle the authentication, download, install, and uninstall of entries from a LANCommander server. Here is a quick example of how one can authenticate to a LANCommander server and install a game to `C:\Games`:

```csharp
var client = new LANCommander.SDK.Client();

await client.AuthenticateAsync("username", "password");

var gameManager = new LANCommander.SDK.GameManager(client, "C:\\Games");

var gameId = "114f653d-ea91-484b-8fe9-8e9bb58bde81";

gameManager.Install(gameId);
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
 - Game patching
 - Dedicated server management/administration
 - Redistributable management and distribution
 - IPX Beacon for emulators such as DosBox
 - Game media management and automatic lookup (covers, icons, backgrounds)

The following features are being considered:

 - Linux build
 - Some expansion of the dashboard with useful stats
 - Built-in VPN client/server for remote LAN parties