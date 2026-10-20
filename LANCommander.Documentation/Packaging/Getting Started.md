---
sidebar_label: Getting Started
sidebar_position: 2
---

# Getting Started

## Requirements

- **Windows.** The hook DLL that records file and registry activity is Windows-only.
- **An account with permission to create games** on the connected server. Without it the packaging entry point is not shown, because the server would reject the upload anyway.
- **The game's original installer.**

Nothing extra to install: the packaging workers ship inside the launcher, under `Packaging\win-x64\` and `Packaging\win-x86\` next to the launcher executable.

## Opening the wizard

Open the profile menu in the top right of the launcher and choose **Package a Game...**.

If you do not see the entry:

- You are not on Windows.
- Your account is not an administrator on the server.
- The launcher is in offline mode.
- The worker binaries are missing from the install. The launcher logs which workers it found at startup — check the log for `Packaging worker missing`.

## Elevation

Most installers ask for administrator rights, and a process running at normal privilege cannot monitor one that is elevated.

The capture starts unelevated. If the installer needs elevation, monitoring reports it and offers **Restart as administrator**. Accepting shows one UAC prompt and restarts the capture with elevated workers; anything already captured is kept.

If you decline, monitoring continues, but any part of the install that runs elevated will not be recorded.

## What gets captured

Only changes that matter for packaging:

- Files the installer **wrote**, copied or moved. Reads are ignored — an installer reads thousands of files it did not create.
- Registry keys **created** and values **written**.
- Anything under the Windows directory or a temp folder is discarded.

Two limits are worth knowing up front:

- **Registry value data is not captured.** The hooks report which keys and values were written, but not the data. Generated install scripts recreate the keys and values with empty values, so fill in anything that matters before publishing.
- **Very short-lived child processes can be missed.** Instrumenting a child means noticing it and injecting into it, and a process that starts and finishes between polls can slip through. The file selection step compensates by also scanning the install folder afterwards and offering anything it finds that monitoring did not see.

Monitoring warns you when either of these actually affected a capture, rather than leaving you to find out later.
