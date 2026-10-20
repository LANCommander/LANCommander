---
sidebar_label: Wizard Walkthrough
sidebar_position: 3
---

# Wizard Walkthrough

Seven steps take an installer to a finished package. The current step is shown above the content.

## 1. Monitor

Choose the installer and start. The launcher runs it with monitoring attached and shows running counts of files, registry changes and processes seen.

Complete the install as you normally would. When the installer exits, monitoring stops on its own — or press **Stop monitoring** if it leaves a helper running, or you cancelled partway through.

Warnings appear here when a capture is incomplete: processes that could not be monitored, or events dropped because the installer produced them faster than they could be recorded.

Installers that ask for administrator rights escalate out of reach of the monitoring, so the capture has to be restarted elevated. The step shows a prompt, and a desktop notification is raised at the same time with the same **Restart as Administrator** action — the launcher is rarely the window in front at that moment, and the installer's own consent dialog is usually covering it. Turn the notification off under **Settings → Notifications → Packaging Needs Administrator Rights**.

## 2. Install Folder

Where the game ended up. This is detected from the common ancestor of every file the installer wrote — not the folder with the most files in it, which for most installers is a subfolder like `Sounds\`.

Every path in the package is stored relative to this folder, so it must be the game's root.

## 3. Additional Changes

Where the work that happens *after* the installer finishes gets recorded: no-CD patches, widescreen fixes, edited configs, mods.

Entering the step takes a baseline of the install folder. Everything from that point on is measured against it.

**Changes you make by hand.** Press **Browse** to open the install folder, apply your patches, then press **Rescan**. The scan lists what was added, changed and removed, with the size difference for anything modified — which is usually how you confirm a patch actually landed. Rescan as often as you like; it reads no file contents, only one walk of the directory tree.

**Patches that come as their own installer.** Use **Run & Monitor** instead. It runs the executable you pick under the same monitoring the base install used, so registry writes and anything it puts outside the install folder are captured too, and merged into one change set. The folder is rescanned automatically when it finishes. Run as many as you need — each one is listed as you go.

**Reset** treats the folder as it stands now as the new starting point, for when you have already reviewed one round of patching and want the next round on its own.

Files this step finds are marked `added` or `changed` in the file step, so your patches are findable among the thousands of files the installer produced.

Changes are detected from each file's size and last-write time. A patcher that rewrites a file to exactly the same length *and* restores its original timestamp will not show up in a rescan — run that patcher through **Run & Monitor** instead, which sees the writes regardless.

Games that need no patching can pass straight through.

## 4. Files

A tri-state tree of everything that will go into the archive. Checking a folder checks everything under it.

This step also scans the install folder and adds anything that monitoring did not see, pre-checked, with a note saying how many were found. That is the main safety net for installers whose child processes could not be instrumented in time.

Files that changed after the base install finished carry a badge: `added` for something new to the folder, `changed` for a file the installer wrote that has since been rewritten. Captured files that no longer exist — deleted by a patch, or by you — are left out entirely rather than sitting checked and contributing nothing.

## 5. Registry

The captured registry keys and values, as a tree. `+` marks a key that was created, `~` a value that was written.

Values written by 32-bit processes are labelled `(32-bit)`. These are physically stored under `WOW6432Node`, and the generated scripts target that location — a script that wrote to the 64-bit view instead would leave the game unable to find its own settings.

Games that touch no registry can pass straight through.

## 6. Details

Title, version, release date, description and notes.

**Look up...** searches the server's configured metadata providers and fills in the description, release date and the richer collections — genres, tags, developers, publishers, platforms, multiplayer modes — that would be tedious to enter by hand.

## 7. Actions

The entry points the launcher offers for the game, edited as a table with the same columns as the server's action editor.

One action is created for you, pointing at the likeliest executable — for most games that is the whole step. Press **Add action** for anything else the game ships: a separate multiplayer executable, a dedicated server, a configuration tool.

| Column | |
|---|---|
| **Name** | What the launcher shows in its menu. |
| **Path** | The executable, relative to the install folder. Pick from the list or type a path the file step never offered — something a script creates at install time, say. A warning marks a path that is not among the files going into the package. |
| **Arguments** | Passed to the executable. Optional. |
| **Working directory** | Defaults to `{InstallDir}`, which the launcher expands to wherever it installed the game. |
| **Primary** | The action the Play button runs. Exactly one is primary; ticking another moves it. |

The play button on a row runs that action right now, against the game as it sits on disk, so a wrong path or a bad argument turns up here rather than after someone installs the package. Nothing is reported when it works — the game appearing is the confirmation; a message only shows up when it does not. Only `{InstallDir}` is expanded — the display and server variables come from a live session that does not exist yet. Note that launching the game writes its own config and save files into the install folder; if you go back to **Files** afterwards they will be swept in, so uncheck anything you did not mean to ship.

Use the arrows to reorder. The order here is the order the launcher lists them in.

Installers, uninstallers and redistributables are filtered out of the path lists by default; tick **Show every executable** if the one you want is hidden.

## 8. Finish

Two independent choices:

- **Save an .lcx file** — writes the package to disk, to import later or keep as a backup.
- **Publish to the connected server** — uploads it and imports it, creating the game. Only available to accounts that may create games.

Pick either, or both. Publishing without saving builds the package to a temporary file and cleans it up afterwards; a file you asked to save is never deleted.

Under **Options**: the GameSpy/OpenSpy patch, and the compression level for the archive.
