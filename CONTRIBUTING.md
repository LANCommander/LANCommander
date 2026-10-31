# Contributing to LANCommander

Thanks for your interest in contributing to LANCommander! This project is primarily developed by a single developer, so community contributions are greatly appreciated.

## Getting Started

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js](https://nodejs.org/) (for the server UI's TypeScript/SCSS assets)
- A code editor such as [Visual Studio](https://visualstudio.microsoft.com/), [Rider](https://www.jetbrains.com/rider/), or [VS Code](https://code.visualstudio.com/)

### Building the Project

1. Clone the repository:
   ```bash
   git clone https://github.com/LANCommander/LANCommander.git
   cd LANCommander
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the server:
   ```bash
   dotnet build LANCommander.Server
   ```

4. Build the launcher:
   ```bash
   dotnet build LANCommander.Launcher
   ```

### Running Locally

To run the server in development mode:
```bash
dotnet run --project LANCommander.Server
```

The server will be available at `http://localhost:1337` by default.

To run the launcher:
```bash
dotnet run --project LANCommander.Launcher -f net10.0-windows10.0.19041.0
```

The launcher multi-targets `net10.0` and `net10.0-windows10.0.19041.0`, so `-f` is required. Each target framework keeps its own `bin/Debug/<framework>/Data` folder, meaning settings, database and login session don't carry over when you switch between them.

#### Reporting a different version

The launcher sends its version to the server on every request and refuses to talk to a server with a different major version. A source build reports the version in `Directory.Build.props`, which won't match a server on another release. Set `LANCOMMANDER_VERSION` to override what the launcher reports:

```bash
LANCOMMANDER_VERSION=2.1.11 dotnet run --project LANCommander.Launcher -f net10.0-windows10.0.19041.0
```

`LANCommander.Launcher/Properties/launchSettings.json` presets this for IDE debugging — edit the value there to match the server you're testing against. The variable is read at runtime, so no rebuild is needed.

#### Release version synchronization

`Directory.Build.props` is the single source of .NET build versions, including the
launcher and the SDK, server plugin contracts and UI NuGet projects. `Version`
tracks the release tag without `v` (for example `2.2.0-alpha.3`); `AssemblyVersion`
is always numeric (`2.2.0.0`), matching release builds. The frontend `package.json`
and lockfile versions describe webpack asset projects, not .NET identities or
published npm packages, and are not synchronized. Dependency versions in
`Directory.Packages.props` are also unchanged.

`LANCommander.Version.Sync.yml` opens a reviewable, version-specific PR against the
default branch when a version release is published. The tag release pipeline also
calls it after successfully creating its draft release, because events created
with `GITHUB_TOKEN` do not start other workflows. The workflow can be rerun or
manually dispatched with an existing release tag to recover a missed update.
Nightly and VLC release events are ignored; malformed version tags fail explicitly.
The workflow checks out the default branch, not the tagged tree, so it retains
current code. It updates only the two root version properties, never bumps a future
patch, refuses to downgrade SemVer precedence, and reuses the same PR branch on
reruns. Merge the PR to update ordinary local builds; already-built assemblies
must be rebuilt. A rerun after merging is a no-op.

This uses `GITHUB_TOKEN` with `contents: write` and `pull-requests: write`; no new
secret is required. Enable **Allow GitHub Actions to create and approve pull
requests** in repository **Settings > Actions > General > Workflow permissions**.
PRs created with `GITHUB_TOKEN` do not automatically trigger PR workflows; use an
approved GitHub App token instead if automatic PR CI is required, or run the
existing manually dispatchable CI workflow before merging. The synchronization PR
itself does not publish packages or create tags.

To synchronize or verify locally with PowerShell 7 (no external test modules):

```powershell
pwsh -File LANCommander.Build/Sync-ReleaseVersion.ps1 -ReleaseTag v2.2.0-alpha.3
pwsh -File LANCommander.Build/Test-SyncReleaseVersion.ps1
```

Tests cover stable/prerelease/build-metadata tags, numeric assembly components,
invalid input, older releases, unchanged unrelated content, and byte-identical
reruns. `LANCommander.Version.Tests.yml` runs them on relevant pull requests.

## How to Contribute

### Reporting Bugs

Use the [GitHub Issues](https://github.com/LANCommander/LANCommander/issues) page with the bug report template. Include:
- Steps to reproduce the issue
- Expected vs. actual behavior
- Your OS and LANCommander version
- Relevant logs or screenshots

### Submitting Changes

1. Fork the repository
2. Create a feature branch from `main` (`git checkout -b my-feature`)
3. Make your changes
4. Test your changes locally
5. Commit with a clear, descriptive message
6. Push to your fork and open a Pull Request

### What to Work On

- Check [open issues](https://github.com/LANCommander/LANCommander/issues) for bugs or feature requests
- Documentation improvements are always welcome at our [documentation site](https://docs.lancommander.app/)
- Game packaging scripts and guides for the community

### Code Guidelines

- Follow existing code style and conventions in the project
- Keep PRs focused, one feature or fix per PR when possible
- Include screenshots in your PR if you're changing UI

## Project Structure

| Directory | Description |
|-----------|-------------|
| `LANCommander.Server` | ASP.NET Blazor web application (server/admin) |
| `LANCommander.Launcher` | Avalonia desktop client (launcher) |
| `LANCommander.Packaging` | Packaging domain logic (LCX building, script generation) |
| `LANCommander.Packaging.Worker` | Out-of-process API-hook monitor, published per architecture |
| `LANCommander.SDK` | .NET SDK for building custom clients |
| `LANCommander.Server.Data` | Entity Framework data models and migrations |
| `LANCommander.Server.Services` | Server business logic |
| `LANCommander.Documentation` | Docusaurus documentation site |

## Community

- [Discord](https://discord.gg/vDEEWVt8EM): Best place for discussion, help, and sharing game packages

## License

By contributing to LANCommander, you agree that your contributions will be licensed under the [MIT License](LICENSE).
