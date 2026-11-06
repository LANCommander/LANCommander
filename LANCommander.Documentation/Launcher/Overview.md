# Launcher
## Overview
The official LANCommander launcher is a desktop application built in ASP.NET Blazor and was designed to be easy to use and familiar. By providing a custom launcher, LANCommander is able to provide a tight integration between the server and any authenticated client.

![Launcher Library View](./_Assets/Library.png)

## Logging In
When opening the launcher for the first time, users will be presented with an authentication screen. From here they will have to enter the LANCommander server address and their credentials. If the launcher is running on the same network as the server, the **Discovered Servers** pane will show any available servers (if beaconing is enabled. See [Server / Settings](/docs/Server/Settings) for more details).

![Launcher Login View](./_Assets/Logging%20In.png)

If the user does not have an account registered on the server, they can register either directly in-launcher by clicking the **Register** button, or the server's web UI.

After logging in, the launcher will immediately start syncing the user's accessible games from the server. Once the sync is complete the user is free to start installing any games listed.

## Changing the Install Location
By default, the launcher will install games to `C:\Games` on Windows. This can be changed in settings by hovering over your username in the top right and clicking **Settings** in the dropdown.

![Launcher Settings](./_Assets/Settings.png)

If you have more than one storage path specified for games, you will be asked upon install which path you would like to install the game to.

![Launcher Install Directory Location](./_Assets/Install%20Directory%20Location.png)

## Filtering Games
By clicking the filter icon in the bottom right, you can filter games based on specific criteria. This metadata is pulled in directly from the game's configuration on the server.

![Launcher Library Filter](./_Assets/Filter.png)

## Download Queue
The launcher has the ability to queue up multiple game installs by utilizing a download queue. Simply start installing multiple games and click the bottom bar to see the queue's current progress.

![Launcher Download Queue](./_Assets/Download%20Queue.png)

## Seeing Who's Playing
While someone on the server is in a game, their name is highlighted in chat, and the game's page lists them under **Playing Now**. Hovering over a highlighted name (or moving to it with a controller) shows a card with the game's icon, its title, and how long they have been playing.

Games run through the LANCommander Interposer can report more: the map, the score, or how full the party is. The card shows these too, whether or not Discord is running.

To choose what others see, open the menu under your name and pick an option under **Activity Status**:

- **Share what I'm playing**: the game and everything it reports. This is the default.
- **Share only the game**: the game and how long you have been playing, without the details it reports.
- **Don't share what I'm playing**: nothing. You never show as playing.

The menu offers this only while the launcher is connected to a server that supports it.

## Refreshing Games
If a new game is available or a user's list of accessible games has changed, click the import button at the top of the application next to the user's name to sync the list of games from the server.

![Launcher Refreshing Games](./_Assets/Refreshing%20Games.png)

## Script Debugging
Developing scripts for games is often a process of trial and error. When the setting **Enable Script Debugging** is enabled in the launcher, users will be given the ability to execute any configured scripts for a game without having to reinstall.

![Launcher Manual Script Execution](./_Assets/Manual%20Script%20Execution.png)

Additionally, a terminal will show at the bottom of the launcher after the script has executed. This is a limited PowerShell runspace and will allow for basic debugging of scripts and provides a history of the execution. Any variables that were passed into the script are also listed for visibility.

![Launcher Script Debugger](./_Assets/Script%20Debugger.png)