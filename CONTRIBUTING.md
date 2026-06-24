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
| `LANCommander.Packager` | Game packaging tool |
| `LANCommander.SDK` | .NET SDK for building custom clients |
| `LANCommander.Server.Data` | Entity Framework data models and migrations |
| `LANCommander.Server.Services` | Server business logic |
| `LANCommander.Documentation` | Docusaurus documentation site |

## Community

- [Discord](https://discord.gg/vDEEWVt8EM): Best place for discussion, help, and sharing game packages

## License

By contributing to LANCommander, you agree that your contributions will be licensed under the [MIT License](LICENSE).
