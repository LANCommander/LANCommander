---
id: GettingStarted
sidebar_label: Getting Started
sidebar_position: 2
---

# Getting Started

This guide will walk you through setting up LANCommander from scratch — from installing the server to connecting your first client and adding games to your library.

---

## 1. Install the Server

The LANCommander server is available as pre-built binaries for Windows, Linux, and macOS (x86 and ARM), as well as a Docker container.

### Docker (Recommended)

The easiest way to get started is with Docker. See the [Docker deployment guide](/Server/Installation/Docker) for a full walkthrough, including a sample `docker-compose.yml`.

### Binary

1. Download the latest release for your platform from the [GitHub Releases page](https://github.com/LANCommander/LANCommander/releases).
2. Extract the archive to a directory of your choice (e.g. `C:\LANCommander` on Windows or `/opt/lancommander` on Linux).
3. Run the server executable:
   - **Windows:** `LANCommander.Server.exe`
   - **Linux / macOS:** `./LANCommander.Server`
4. The server will start and listen on port **1337** by default.

---

## 2. Initial Server Setup

Once the server is running, open a browser and navigate to `http://<server-address>:1337`.

On first launch you will be prompted to create an administrator account. Fill in a username and password, then click **Create**. These credentials will be used to log in to the server's web interface.

After creating your account you will be taken to the main dashboard where you can begin configuring your library.

---

## 3. Install the Launcher

The LANCommander launcher is the desktop client your users will use to browse, install, and play games.

1. Download the latest launcher release for your platform from the [GitHub Releases page](https://github.com/LANCommander/LANCommander/releases).
2. Extract and run the launcher executable.

---

## 4. Connect the Launcher to the Server

When the launcher opens for the first time you will be presented with a login screen.

1. Enter the address of your LANCommander server (e.g. `http://192.168.1.100:1337`).
   - If the launcher is on the same network as the server and beaconing is enabled, the server will appear automatically in the **Discovered Servers** list.
2. Enter your username and password, then click **Login**.
   - If you don't have an account yet, click **Register** to create one (requires registration to be enabled on the server).
3. After logging in, the launcher will sync your accessible game library from the server.

---

## 5. Add Games to the Library

Games are managed from the server's web interface.

1. Log in to the server at `http://<server-address>:1337`.
2. Navigate to **Games** in the sidebar.
3. Click **Add Game** and fill in the game's details (title, metadata, cover art, etc.).
4. Upload the game archive or point to an existing archive on disk.
5. Optionally configure [scripts](/Scripting/Overview) (install, uninstall, key change, etc.) for the game.

Once a game is added and made accessible to users, it will appear in the launcher after the next sync.

---

## Next Steps

- [Server Documentation](/Server/Overview) — detailed server configuration, redistributables, collections, and more
- [Launcher Documentation](/Launcher/Overview) — launcher features including the download queue, filtering, and script debugging
- [Scripting](/Scripting/Overview) — automate game setup with PowerShell scripts
- [SDK Documentation](/SDK/Overview) — integrate LANCommander into your own applications
