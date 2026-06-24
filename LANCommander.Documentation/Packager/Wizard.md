---
sidebar_label: Wizard Walkthrough
sidebar_position: 3
---

# Wizard Walkthrough

The Packager walks you through seven steps to create a complete `.LCX` package. Each step is shown in the sidebar with a progress indicator.

---

## Step 1: Monitor Installer

After selecting an installer executable, the Packager launches it and monitors all file and registry activity using native DLL injection (Interposer). A real-time log displays captured events as the installer runs.

The Packager automatically:
- Detects the installer's architecture (32-bit or 64-bit) and injects the appropriate Interposer DLL
- Monitors child processes spawned by the installer
- Filters out writes to system directories (Windows, temp folders)
- Captures both file writes and registry key/value creation

Once the installer exits, the captured data is summarized in the status bar. Click **Next** to continue.

:::info
The log view continues to show captured events for reference. All diagnostic output is also written to `packager.log` in the application directory.
:::

---

## Step 2: Install Directory

The Packager analyzes the captured file writes to detect the game's installation directory. This is determined by finding the most common non-system directory among the written files.

If the detected directory is incorrect, click **Browse** to manually select the correct location. This directory becomes the root of the game archive.

---

## Step 3: Select Files

All files within the install directory are displayed in a tree view with checkboxes. By default, every file is selected.

- **Check/uncheck a directory** to toggle all files within it
- **Select All** / **Select None** buttons at the top for bulk operations
- The counter at the top shows how many files are currently selected

Files outside the install directory (if any were captured) are listed by their full paths. Only files that still exist on disk at this point are shown.

---

## Step 4: Registry Entries

All captured registry writes are displayed in a tree view organized by hive and key path. Entries are deduplicated so if the same key and value were written multiple times during installation, only one entry is shown.

Each leaf entry displays an indicator:
- **Green +** - the entry was created during installation
- **Yellow ~** - the entry was updated (written to an existing key)

Selected entries will be included in the auto-generated install and uninstall scripts. The install script recreates the registry keys and values; the uninstall script removes them.

---

## Step 5: Game Metadata

Enter basic information about the game. The title is pre-populated from the installer's filename.

| Field | Description |
|:------|:------------|
| **Title** | Display name of the game (required) |
| **Sort Title** | Optional override for alphabetical sorting |
| **Version** | Game version, defaults to `1.0` |
| **Released On** | Release date of the game |
| **Singleplayer** | Whether the game supports singleplayer |
| **Description** | A description of the game |
| **Notes** | Private notes (admin-only, not shown to users) |

---

## Step 6: Game Executable

The Packager scans your selected files for `.exe` files and filters out common installer/redistributable executables (e.g. `vcredist`, `dxsetup`, `setup`, `unins`). The remaining executables are displayed in a list.

Select the primary game executable. This is the file the launcher will run when the user clicks "Play". You can also customize:

| Field | Description |
|:------|:------------|
| **Action Name** | Label shown on the play button, defaults to `Play` |
| **Arguments** | Command-line arguments passed when launching |

---

## Step 7: Generate Package

Configure the output path for the `.LCX` file and optionally adjust packaging options before generating.

### Output Path

The default output path is based on the game title in the current working directory. Click **Browse** to choose a different location.

### Options

Expand the **Options** panel to configure additional settings:

| Option | Description |
|:-------|:------------|
| **Patch GameSpy** | Adds an `Edit-PatchGameSpy -Path $InstallDirectory` call to the install script. This scans the install directory for GameSpy references and patches them for OpenSpy compatibility. |
| **Compression Level** | Controls the trade-off between archive size and packaging speed. Options: Optimal (default), Fastest, No Compression, Smallest Size. |
| **Write Summary Log** | Writes a `.Package.log` file alongside the `.LCX` output documenting the source installer, selected files, registry entries, metadata, and options used. |

Click **Generate .LCX** to build the package. A progress bar shows the current stage:
1. Creating game files archive
2. Generating scripts
3. Writing manifest

On completion, the output path and file size are displayed.
