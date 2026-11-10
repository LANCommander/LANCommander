using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Microsoft.Extensions.DependencyInjection;
using Manifest = LANCommander.SDK.Models.Manifest;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Tool and server packages carry actions too. The action importer only accepts actions when the
/// package is a game, so these may be dropped (or, for servers, come in some other way).
/// </summary>
[Collection("BUnit")]
public class ToolAndServerImportTests
{
    private readonly BUnitServerFixture _fixture;

    public ToolAndServerImportTests(BUnitServerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task ExportedToolRoundTripsItsActions()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var toolService = services.GetRequiredService<ToolService>();
        var actionService = services.GetRequiredService<ActionService>();

        var tool = await toolService.AddAsync(new Tool { Name = $"Tool {Guid.NewGuid():N}" });
        var action = await actionService.AddAsync(new Data.Models.Action { ToolId = tool.Id, Name = "Run", Path = "tool.exe", Type = ActionType.Primary });

        var exportContext = services.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(tool.Id, ImportExportRecordType.Tool)).ToList();

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(ImportPackages.TempDirectory(), "tool.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        // Importing onto a different server
        await actionService.DeleteAsync(action);
        await toolService.DeleteAsync(await toolService.GetAsync(tool.Id));

        await services.GetRequiredService<ImportRunner>().RunFileAsync(exportPath, manifestType: ManifestType.Tool);

        var actions = await actionService.GetAsync(a => a.ToolId == tool.Id);

        Assert.Equal(["tool.exe"], actions.Select(a => a.Path));
    }

    [Fact]
    public async Task ReimportingAToolDoesNotDuplicateItsActions()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = new Manifest.Tool
        {
            ManifestVersion = ImportPackages.ManifestVersion,
            Id = Guid.NewGuid(),
            Name = $"Tool {Guid.NewGuid():N}",
            Actions = [new Manifest.Action { Name = "Run", Path = "tool.exe", Type = ActionType.Primary }],
        };

        var path = await ImportPackages.WriteAsync(manifest);
        var importRunner = services.GetRequiredService<ImportRunner>();

        await importRunner.RunFileAsync(path, manifestType: ManifestType.Tool);
        await importRunner.RunFileAsync(path, manifestType: ManifestType.Tool);

        var actions = await services.GetRequiredService<ActionService>().GetAsync(a => a.ToolId == manifest.Id);

        Assert.Equal(["tool.exe"], actions.Select(a => a.Path));
    }

    private static Manifest.Server ServerManifest() => new()
    {
        ManifestVersion = ImportPackages.ManifestVersion,
        Id = Guid.NewGuid(),
        Name = $"Server {Guid.NewGuid():N}",
        Path = "server.exe",
        WorkingDirectory = ImportPackages.TempDirectory(),
        Engine = "Local",
        // Never null in an export: the column isn't nullable, and an empty host exports as ''
        Host = "",
        Actions = [new Manifest.Action { Name = "Connect", Path = "client.exe", Arguments = "+connect {ServerHost}", Type = ActionType.Primary }],
        ServerConsoles =
        [
            new Manifest.ServerConsole { Name = "Log", Type = ServerConsoleType.LogFile, Path = "logs/server.log" },
            new Manifest.ServerConsole { Name = "RCON", Type = ServerConsoleType.RCON, Host = "127.0.0.1", Port = 27015 },
        ],
        HttpPaths =
        [
            new Manifest.ServerHttpPath { LocalPath = "maps", Path = "/maps" },
            new Manifest.ServerHttpPath { LocalPath = "sounds", Path = "/sounds" },
        ],
    };

    private async Task<(List<string> Consoles, List<string> HttpPaths)> ConsolesAndHttpPathsAsync(IServiceProvider services, Guid serverId)
    {
        var consoles = await services.GetRequiredService<ServerConsoleService>().GetAsync(c => c.ServerId == serverId);
        var httpPaths = await services.GetRequiredService<ServerHttpPathService>().GetAsync(p => p.ServerId == serverId);

        return (consoles.Select(c => c.Name).Order().ToList(), httpPaths.Select(p => p.Path).Order().ToList());
    }

    /// <summary>
    /// Consoles and HTTP paths are added by their own importers once the server exists, not as part
    /// of creating the server.
    /// </summary>
    [Fact]
    public async Task ServerPackageImportsItsConsolesAndHttpPathsOnce()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = ServerManifest();
        var path = await ImportPackages.WriteAsync(manifest);
        var importRunner = services.GetRequiredService<ImportRunner>();

        await importRunner.RunFileAsync(path, manifestType: ManifestType.Server);

        var (consoles, httpPaths) = await ConsolesAndHttpPathsAsync(services, manifest.Id);

        Assert.Equal(["Log", "RCON"], consoles);
        Assert.Equal(["/maps", "/sounds"], httpPaths);

        await importRunner.RunFileAsync(path, manifestType: ManifestType.Server);

        (consoles, httpPaths) = await ConsolesAndHttpPathsAsync(services, manifest.Id);

        Assert.Equal(["Log", "RCON"], consoles);
        Assert.Equal(["/maps", "/sounds"], httpPaths);
    }

    /// <summary>
    /// Continuing an import from the upload indicator doesn't say what the package is, so its type
    /// is worked out from the manifest. Server, tool and redistributable manifests all have a Name.
    /// </summary>
    [Fact]
    public async Task ServerPackageWithoutATypeImportsAsAServer()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = ServerManifest();

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await ImportPackages.WriteAsync(manifest));

        Assert.Equal(ManifestType.Server, result.ManifestType);
        Assert.NotNull(await services.GetRequiredService<ServerService>().GetAsync(manifest.Id));
    }

    [Fact]
    public async Task ToolPackageWithoutATypeImportsAsATool()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = new Manifest.Tool
        {
            ManifestVersion = ImportPackages.ManifestVersion,
            Id = Guid.NewGuid(),
            Name = $"Tool {Guid.NewGuid():N}",
            Actions = [new Manifest.Action { Name = "Run", Path = "tool.exe", Type = ActionType.Primary }],
        };

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await ImportPackages.WriteAsync(manifest));

        Assert.Equal(ManifestType.Tool, result.ManifestType);
        Assert.NotNull(await services.GetRequiredService<ToolService>().GetAsync(manifest.Id));
    }

    [Fact]
    public async Task RedistributablePackageWithoutATypeImportsAsARedistributable()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = new Manifest.Redistributable
        {
            ManifestVersion = ImportPackages.ManifestVersion,
            Id = Guid.NewGuid(),
            Name = $"Redistributable {Guid.NewGuid():N}",
        };

        var result = await services.GetRequiredService<ImportRunner>().RunFileAsync(await ImportPackages.WriteAsync(manifest));

        Assert.Equal(ManifestType.Redistributable, result.ManifestType);
        Assert.NotNull(await services.GetRequiredService<RedistributableService>().GetAsync(manifest.Id));
    }

    /// <summary>Two servers can serve the same URL path; re-importing one must not take the other's.</summary>
    [Fact]
    public async Task ReimportingAServerLeavesOtherServersHttpPathsAlone()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;
        var importRunner = services.GetRequiredService<ImportRunner>();

        var first = ServerManifest();
        var second = ServerManifest();

        await importRunner.RunFileAsync(await ImportPackages.WriteAsync(first), manifestType: ManifestType.Server);

        var secondPath = await ImportPackages.WriteAsync(second);
        await importRunner.RunFileAsync(secondPath, manifestType: ManifestType.Server);
        await importRunner.RunFileAsync(secondPath, manifestType: ManifestType.Server);

        Assert.Equal(["/maps", "/sounds"], (await ConsolesAndHttpPathsAsync(services, first.Id)).HttpPaths);
        Assert.Equal(["/maps", "/sounds"], (await ConsolesAndHttpPathsAsync(services, second.Id)).HttpPaths);
    }

    [Fact]
    public async Task ServerPackageImportsItsActionsOnce()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = ServerManifest();

        await services.GetRequiredService<ImportRunner>().RunFileAsync(await ImportPackages.WriteAsync(manifest), manifestType: ManifestType.Server);

        var actions = await services.GetRequiredService<ActionService>().GetAsync(a => a.ServerId == manifest.Id);

        Assert.Equal(["client.exe"], actions.Select(a => a.Path));
    }

    [Fact]
    public async Task ReimportingAServerDoesNotDuplicateItsActions()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();
        var services = scope.ServiceProvider;

        var manifest = ServerManifest();
        var path = await ImportPackages.WriteAsync(manifest);
        var importRunner = services.GetRequiredService<ImportRunner>();

        await importRunner.RunFileAsync(path, manifestType: ManifestType.Server);
        await importRunner.RunFileAsync(path, manifestType: ManifestType.Server);

        var actions = await services.GetRequiredService<ActionService>().GetAsync(a => a.ServerId == manifest.Id);

        Assert.Equal(["client.exe"], actions.Select(a => a.Path));
    }
}
