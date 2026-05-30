---
sidebar_label: LCX Package Format
sidebar_position: 4
---

# LCX Package Format

An `.LCX` file is a standard ZIP archive containing everything needed to install and configure a game through LANCommander. The Packager generates this format automatically, but understanding its structure is useful for troubleshooting or manual editing.

## Archive Structure

```
package.lcx (ZIP)
├── manifest.yaml           # Game metadata (YAML)
├── Archives/
│   └── {guid}              # Inner ZIP containing game files
└── Scripts/
    ├── {guid}              # Install script (PowerShell)
    └── {guid}              # Uninstall script (PowerShell)
```

### manifest.yaml

The manifest is a YAML file describing the game's metadata, actions, archive references, and script references. It follows the LANCommander SDK's `Game` manifest schema and includes:

- **Title, Sort Title, Version, Description, Notes** - basic metadata
- **Released On, Singleplayer** - classification
- **Directory Name** - the expected install directory name
- **Actions** - launch configurations (name, executable path, arguments, primary flag)
- **Archives** - references to inner archive entries with compressed/uncompressed sizes
- **Scripts** - references to script entries with type (Install/Uninstall) and admin requirements

### Archives

The `Archives/` directory contains one or more inner ZIP files, each identified by a GUID. The inner archive holds the game files with paths relative to the install directory root.

### Scripts

The `Scripts/` directory contains PowerShell scripts identified by GUID. The Packager generates up to two scripts:

**Install Script** - Recreates registry keys and values captured during monitoring. If the Patch GameSpy option was enabled, it also includes an `Edit-PatchGameSpy` call. Scripts assume `$InstallDirectory` is available in the execution environment (provided by the launcher's PowerShell runtime).

**Uninstall Script** - Removes the registry keys and values that were created by the install script.

## Importing into LANCommander

`.LCX` packages can be imported directly through the LANCommander server's web interface. The server reads the manifest, extracts the archive and scripts, and creates the corresponding game entry with all metadata, actions, and scripts pre-configured.
