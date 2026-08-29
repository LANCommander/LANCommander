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

## 2. Install Folder

Where the game ended up. This is detected from the common ancestor of every file the installer wrote — not the folder with the most files in it, which for most installers is a subfolder like `Sounds\`.

Every path in the package is stored relative to this folder, so it must be the game's root.

## 3. Files

A tri-state tree of everything that will go into the archive. Checking a folder checks everything under it.

This step also scans the install folder and adds anything that monitoring did not see, pre-checked, with a note saying how many were found. That is the main safety net for installers whose child processes could not be instrumented in time.

## 4. Registry

The captured registry keys and values, as a tree. `+` marks a key that was created, `~` a value that was written.

Values written by 32-bit processes are labelled `(32-bit)`. These are physically stored under `WOW6432Node`, and the generated scripts target that location — a script that wrote to the 64-bit view instead would leave the game unable to find its own settings.

Games that touch no registry can pass straight through.

## 5. Details

Title, version, release date, description and notes.

**Look up...** searches the server's configured metadata providers and fills in the description, release date and the richer collections — genres, tags, developers, publishers, platforms, multiplayer modes — that would be tedious to enter by hand.

## 6. Launch Action

Which executable the launcher runs to start the game. Installers, uninstallers and redistributables are filtered out of the list by default; tick **Show every executable** if the one you want is hidden.

## 7. Finish

Two independent choices:

- **Save an .lcx file** — writes the package to disk, to import later or keep as a backup.
- **Publish to the connected server** — uploads it and imports it, creating the game. Only available to accounts that may create games.

Pick either, or both. Publishing without saving builds the package to a temporary file and cleans it up afterwards; a file you asked to save is never deleted.

Under **Options**: the GameSpy/OpenSpy patch, and the compression level for the archive.
