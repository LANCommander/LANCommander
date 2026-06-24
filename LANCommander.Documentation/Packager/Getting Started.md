---
sidebar_label: Getting Started
sidebar_position: 2
---

# Getting Started

## Requirements

- **Windows 10 or later** (x86 or x64)
- **Administrator privileges** - the Packager requires elevation to monitor installer processes via DLL injection

The Packager is distributed as a single 32-bit executable (`LANCommander.Packager.exe`). No installation is required.

## Download

Download the latest release from the [GitHub Releases page](https://github.com/LANCommander/LANCommander/releases). The Packager artifact is named `LANCommander.Packager-Windows-x86-v{VERSION}.zip`.

Extract the archive to a directory of your choice and run `LANCommander.Packager.exe`.

## Command-Line Usage

The Packager can optionally accept arguments to skip the initial file picker dialog:

```
LANCommander.Packager.exe [installer-path] [-o output-path]
```

| Argument | Description |
|:--------:|:------------|
| `installer-path` | Path to the installer executable to monitor |
| `-o`, `--output` | Path for the output `.lcx` file |

If no installer path is provided, a file picker dialog will appear on launch.
