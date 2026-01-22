---
title: Package
---

# Overview
LANCommander Package Scripts are PowerShell scripts that run on a recurring schedule to automate the creation of game archives.

Each script is responsible for:

- Locating or generating the files that need to be packaged.
- Preparing a directory that contains all files for the new version.
- Emitting a PowerShell object describing the package output.

LANCommander ingests the object returned by the script, compresses the directory into an archive, and tags it using the version provided. If a script does not return an object, LANCommander assumes that no new package is required.

---

## Required Output Format

Each script must return a **PowerShell object** with the following properties:

| Property   | Type     | Description |
|------------|----------|-------------|
| `Path`     | string   | Full path to a directory containing all files to be archived. |
| `Version`  | string   | Version identifier used to tag the generated archive. |
| `Changelog`| string   | Description of what changed in the new version. |

### Important Behavior

- `Path` **must** be a directory, not a file. Everything under that directory will be compressed automatically.
- `Version` must be a meaningful unique identifier (e.g., `1.0.0`, `2.5.4-beta1`, `2026.01.14`).
- `Changelog` may be multiline text and will be stored with the packaged archive.

Scripts must **return** this object, not write it to a file or log.

---

## Minimal Example Script

```powershell
# Minimal example returning a package definition
return [PSCustomObject]@{
    Path      = "C:\Packages\MyGame\1.0.0"
    Version   = "1.0.0"
    Changelog = "Initial automated release."
}