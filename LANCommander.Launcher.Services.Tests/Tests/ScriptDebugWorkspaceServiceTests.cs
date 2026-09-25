using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services.ScriptDebugging;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell.Debugging;
using LANCommander.SDK.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// The script debugger's view of a game: which scripts it lists, where their text comes from, and where
/// edits go. Installed games are read from the install directory; the server is faked at the catalogue it
/// returns.
/// </summary>
public sealed class ScriptDebugWorkspaceServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"lc-script-ws-{Guid.NewGuid():N}");
    private readonly string _installDirectory;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _redistributableId = Guid.NewGuid();
    private readonly Guid _installScriptId = Guid.NewGuid();

    public ScriptDebugWorkspaceServiceTests()
    {
        _installDirectory = Path.Combine(_root, "Game");
        Directory.CreateDirectory(_installDirectory);

        ScriptDrafts.RootDirectory = Path.Combine(_root, "Drafts");
    }

    public void Dispose()
    {
        ScriptDrafts.RootDirectory = null;

        try { Directory.Delete(_root, recursive: true); }
        catch (Exception) { }
    }

    private DatabaseContext CreateContext(bool installed)
    {
        var context = new DatabaseContext(
            NullLoggerFactory.Instance,
            new DbContextOptionsBuilder().UseInMemoryDatabase($"ScriptWorkspace-{Guid.NewGuid()}").Options);

        var game = GameFactory.Make("Test Game", _gameId, installed: installed);
        game.InstallDirectory = installed ? _installDirectory : string.Empty;

        context.Games!.Add(game);
        context.SaveChanges();

        return context;
    }

    private ScriptDebugWorkspaceService CreateSubject(DatabaseContext context, bool offline = false, ServerScriptCatalog? catalog = null)
    {
        var connection = new Mock<IConnectionClient>();
        connection.Setup(c => c.IsOfflineMode()).Returns(offline);
        connection.Setup(c => c.IsConnected()).Returns(!offline);

        // Only the database and the manifest helpers are exercised; the server-facing graph is null!.
        var gameService = new GameService(
            context, NullLogger<GameService>.Instance, null!, null!, null!, null!, null!, null!, connection.Object, null!, null!);

        var gameClient = new GameClient(
            NullLogger<GameClient>.Instance, null!, null!, null!, null!, connection.Object, null!, null!, null!, null!, null!, null!, null!);

        return new FakeServerWorkspaceService(gameService, gameClient, connection.Object, catalog);
    }

    /// <summary>Stands in for the server: the catalogue it would return, or none (the server was unreachable).</summary>
    private sealed class FakeServerWorkspaceService(
        GameService gameService,
        GameClient gameClient,
        IConnectionClient connection,
        ServerScriptCatalog? catalog)
        : ScriptDebugWorkspaceService(NullLogger<ScriptDebugWorkspaceService>.Instance, gameService, gameClient, null!, null!, null!, connection)
    {
        protected override Task<ServerScriptCatalog?> FetchServerCatalogAsync(Guid gameId, CancellationToken cancellationToken) =>
            catalog is null ? throw new HttpRequestException("unreachable") : Task.FromResult<ServerScriptCatalog?>(catalog);
    }

    private static SDK.Models.Script ServerScript(ScriptType type, string name, string contents) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Name = name,
        Contents = contents,
        UpdatedOn = DateTime.UtcNow,
    };

    private async Task WriteInstalledGameAsync()
    {
        await ManifestHelper.WriteAsync(new Manifest.Game
        {
            Id = _gameId,
            Title = "Test Game",
            Scripts =
            [
                new Manifest.Script { Id = _installScriptId, Type = ScriptType.Install, Name = "Configure", RequiresAdmin = true },
            ],
            Redistributables = [new Manifest.Redistributable { Id = _redistributableId, Name = "Runtime" }],
        }, _installDirectory);

        await ManifestHelper.WriteAsync(new Manifest.Redistributable
        {
            Id = _redistributableId,
            Name = "Runtime",
            Scripts = [new Manifest.Script { Id = Guid.NewGuid(), Type = ScriptType.DetectInstall, Name = "Detect" }],
        }, _installDirectory);

        WriteScript(_gameId, ScriptType.Install, "#Requires -RunAsAdministrator\r\n\r\nWrite-Host 'install'");

        // Not in the manifest, but present on disk: it will run, so it is listed.
        WriteScript(_gameId, ScriptType.BeforeStart, "Write-Host 'before start'");

        WriteScript(_redistributableId, ScriptType.DetectInstall, "$Return = $true");
    }

    private string WriteScript(Guid ownerId, ScriptType type, string contents)
    {
        var path = ScriptHelper.GetScriptFilePath(_installDirectory, ownerId, type);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);

        return path;
    }

    [Fact]
    public async Task InstalledGame_ListsEveryScriptFileThatWillRun()
    {
        await WriteInstalledGameAsync();

        await using var context = CreateContext(installed: true);
        var workspace = await CreateSubject(context).LoadAsync(_gameId);

        workspace.IsInstalled.ShouldBeTrue();
        workspace.InstallDirectory.ShouldBe(_installDirectory);

        var game = workspace.Owners.Single(o => o.Kind == ScriptOwnerKind.Game);
        game.Scripts.Select(s => s.Type).ShouldBe([ScriptType.Install, ScriptType.BeforeStart], ignoreOrder: true);

        var install = game.Scripts.Single(s => s.Type == ScriptType.Install);
        install.Name.ShouldBe("Configure");
        install.RequiresAdmin.ShouldBeTrue();
        install.ServerScriptId.ShouldBe(_installScriptId);
        install.Source.ShouldBe(ScriptSource.Installed);
        install.LocalPath.ShouldBe(ScriptHelper.GetScriptFilePath(_installDirectory, _gameId, ScriptType.Install));
        install.CanRunDirectly.ShouldBeTrue();

        var redistributable = workspace.Owners.Single(o => o.Kind == ScriptOwnerKind.Redistributable);
        redistributable.Id.ShouldBe(_redistributableId);
        redistributable.Scripts.Single().Type.ShouldBe(ScriptType.DetectInstall);

        workspace.OwnerIds.ShouldBe([_gameId, _redistributableId], ignoreOrder: true);
    }

    [Fact]
    public async Task InstalledGame_AlsoListsTheServersScripts_ForAddonsAndToolsThatArentInstalled()
    {
        await WriteInstalledGameAsync();

        var installedAddonId = Guid.NewGuid();
        var missingAddonId = Guid.NewGuid();
        var toolId = Guid.NewGuid();

        // One addon is installed alongside the game; the other, and the tool, are only on the server.
        var main = await ManifestHelper.ReadAsync<Manifest.Game>(_installDirectory, _gameId);
        main!.Addons = [new Manifest.Game { Id = installedAddonId, Title = "Installed Pack", Type = GameType.Expansion }];
        await ManifestHelper.WriteAsync(main, _installDirectory);
        await ManifestHelper.WriteAsync(new Manifest.Game { Id = installedAddonId, Title = "Installed Pack", Type = GameType.Expansion }, _installDirectory);
        WriteScript(installedAddonId, ScriptType.Install, "Write-Host 'installed pack'");

        var catalog = new ServerScriptCatalog("Test Game",
        [
            new(ScriptOwnerKind.Game, _gameId, "Test Game", false, null, [ServerScript(ScriptType.AfterStop, "Newer", "server copy")]),
            new(ScriptOwnerKind.Game, installedAddonId, "Installed Pack", true, null, [ServerScript(ScriptType.Uninstall, "Remove", "server copy")]),
            new(ScriptOwnerKind.Game, missingAddonId, "Missing Pack", true, null, [ServerScript(ScriptType.Install, "Unpack", "Write-Host 'missing pack'")]),
            new(ScriptOwnerKind.Redistributable, _redistributableId, "Runtime", false, null, []),
            new(ScriptOwnerKind.Tool, toolId, "Server Browser", false, null, [ServerScript(ScriptType.BeforeStart, "Launch", "Write-Host 'tool'")]),
        ]);

        await using var context = CreateContext(installed: true);
        var workspace = await CreateSubject(context, catalog: catalog).LoadAsync(_gameId);

        workspace.Message.ShouldBeNull();
        workspace.Owners.Select(o => o.Id).ShouldBe([_gameId, installedAddonId, missingAddonId, _redistributableId, toolId]);

        // What is installed is listed from disk only, so the server's newer scripts for it don't appear.
        workspace.Owners[0].Scripts.ShouldAllBe(s => s.Source == ScriptSource.Installed);
        workspace.Owners[0].Scripts.ShouldNotContain(s => s.Type == ScriptType.AfterStop);
        workspace.Owners[1].Scripts.Single().Source.ShouldBe(ScriptSource.Installed);

        var missing = workspace.Owners[2].Scripts.ShouldHaveSingleItem();
        missing.Source.ShouldBe(ScriptSource.Server);
        missing.Name.ShouldBe("Unpack");
        missing.ServerContents.ShouldBe("Write-Host 'missing pack'");
        missing.CanRunDirectly.ShouldBeFalse();

        var tool = workspace.Owners[4];
        tool.Kind.ShouldBe(ScriptOwnerKind.Tool);
        tool.Scripts.Single().Source.ShouldBe(ScriptSource.Server);

        workspace.OwnerIds.ShouldBe([_gameId, installedAddonId, missingAddonId, _redistributableId, toolId], ignoreOrder: true);
    }

    [Fact]
    public async Task InstalledGame_WhenTheServerCantBeReached_ListsWhatIsInstalledAndSaysSo()
    {
        await WriteInstalledGameAsync();

        await using var context = CreateContext(installed: true);
        var workspace = await CreateSubject(context, catalog: null).LoadAsync(_gameId);

        workspace.Owners.Select(o => o.Id).ShouldBe([_gameId, _redistributableId]);
        workspace.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task NotInstalledAndOffline_ExplainsWhyNothingIsListed()
    {
        await using var context = CreateContext(installed: false);
        var workspace = await CreateSubject(context, offline: true).LoadAsync(_gameId);

        workspace.IsInstalled.ShouldBeFalse();
        workspace.Owners.ShouldBeEmpty();
        workspace.Message.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task SavingAnInstalledScript_WritesTheFileThatRuns()
    {
        await WriteInstalledGameAsync();

        await using var context = CreateContext(installed: true);
        var subject = CreateSubject(context);
        var workspace = await subject.LoadAsync(_gameId);
        var entry = workspace.Scripts.Single(s => s.Type == ScriptType.BeforeStart);

        var saved = await subject.SaveAsync(_gameId, entry, "Write-Host 'edited'");

        saved.Source.ShouldBe(ScriptSource.Installed);
        (await File.ReadAllTextAsync(entry.LocalPath!)).ShouldBe("Write-Host 'edited'");
        File.Exists(entry.DraftPath).ShouldBeFalse();
    }

    [Fact]
    public async Task SavingAServerScript_KeepsADraft_ThatCanBeDiscarded()
    {
        await using var context = CreateContext(installed: false);
        var subject = CreateSubject(context);
        var key = new ScriptKey(_gameId, ScriptType.Install);

        var entry = new ScriptEntry
        {
            Key = key,
            OwnerKind = ScriptOwnerKind.Game,
            OwnerName = "Test Game",
            Name = "Install",
            Source = ScriptSource.Server,
            DraftPath = ScriptDrafts.GetPath(_gameId, key),
            ServerContents = "Write-Host 'server'",
        };

        (await subject.ReadAsync(entry)).ShouldBe("Write-Host 'server'");

        var saved = await subject.SaveAsync(_gameId, entry, "Write-Host 'draft'");

        saved.Source.ShouldBe(ScriptSource.Draft);
        (await subject.ReadAsync(saved)).ShouldBe("Write-Host 'draft'");

        var discarded = subject.DiscardDraft(_gameId, saved);

        discarded.Source.ShouldBe(ScriptSource.Server);
        (await subject.ReadAsync(discarded)).ShouldBe("Write-Host 'server'");
    }

    [Fact]
    public async Task DraftsLeftFromBeforeInstall_AreOfferedAndApplied()
    {
        await WriteInstalledGameAsync();

        var key = new ScriptKey(_gameId, ScriptType.Install);
        await ScriptDrafts.SaveAsync(_gameId, key, "Write-Host 'from the draft'");

        await using var context = CreateContext(installed: true);
        var subject = CreateSubject(context);
        var workspace = await subject.LoadAsync(_gameId);

        var pending = subject.GetPendingDrafts(workspace).ShouldHaveSingleItem();
        pending.Key.ShouldBe(key);

        subject.ApplyDraft(_gameId, pending);

        // The server stores the admin flag separately, so the installed file gets its header back.
        (await File.ReadAllTextAsync(pending.LocalPath!))
            .ShouldBe("#Requires -RunAsAdministrator\r\n\r\nWrite-Host 'from the draft'");

        ScriptDrafts.Exists(_gameId, key).ShouldBeFalse();
        subject.GetPendingDrafts(workspace).ShouldBeEmpty();
    }

    [Fact]
    public void Promote_DoesNothingWithoutADraft()
    {
        var path = WriteScript(_gameId, ScriptType.Install, "original");

        ScriptDrafts.TryPromote(_gameId, new ScriptKey(_gameId, ScriptType.Install), path, requiresAdmin: false).ShouldBeFalse();

        File.ReadAllText(path).ShouldBe("original");
    }
}
