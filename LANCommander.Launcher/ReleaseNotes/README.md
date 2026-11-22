# Launcher release notes

Each release of the launcher has its own markdown file here, named for the release without a
prerelease label: `2.3.0.md`. The files are embedded in the launcher, which shows the notes for every
release newer than the one it updated from in its "What's New" dialog.

- Write the file for the upcoming release as changes land. Release candidates of 2.3.0 show
  `2.3.0.md`, so the notes can grow through the release candidates.
- Write for players, not developers. The dialog already shows the version as a heading, so start
  with the content.
- These are the launcher's notes only. The server keeps its own in `LANCommander.Server/ReleaseNotes`,
  and the full release notes live on https://lancommander.app.

Example `2.3.0.md`:

```markdown
### New

- The launcher updates itself from your server. A badge beside your profile shows when an update
  is available.

### Fixed

- Games with Japanese file names install correctly.
```
