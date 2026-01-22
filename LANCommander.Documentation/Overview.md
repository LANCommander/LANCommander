---
sidebar_label: Overview
sidebar_position: 1
---

# Introduction
LANCommander is an open source digital game distribution platform. In essence, it's a ways to self-host your own game library ala Steam, GOG, Epic, etc.

Both the server and custom launcher applications are built using the ASP.NET Blazor web application framework. Binaries are provided for Windows, Linux, and macOS supporting both x86 and ARM architectures. The server also has a preconfigured Docker container for easier deployment.

The platform is designed to work on local networks and loads no assets from the internet when installing games from the launcher. It was originally developer to help assist deploying games at a LAN party where the local network was closed circuit and no internet access was permitted. The server can also be accessed publicly, though use of a reverse proxy or VPN is recommended.

# Development
Code can be viewed over at the project's [GitHub page](https://github.com/LANCommander/LANCommander). This is where new releases will be posted and also serves as the main issue tracker.

The Docker container is available over at [Docker Hub](https://hub.docker.com/r/lancommander/lancommander) and automatically gets updated with each version release through our CI/CD pipeline.

# Community
The community behind LANCommander is small, but extremely knowledgeable. Most support and troubleshooting questions stem from discussions in the official [Discord server](https://discord.gg/vDEEWVt8EM). There is also a forum within the server where users post freeware and shareware games that can be directly imported into your LANCommander server.

If you would like to support the project, there is a [Patreon page](https://patreon.com/LANCommander) available with a couple paid tiers as well as a free tier. This also serves as a general blog for the project with news about development.

# Installation and Use
This site serves as the main documentation platform for the project. As such, it is recommended to check out the following resources:
- [Getting Started](/GettingStarted)
- [Server](/Server)
- [Launcher](/Launcher)
- [Scripting](/Scripting)
- [SDK Documentation](/SDK)