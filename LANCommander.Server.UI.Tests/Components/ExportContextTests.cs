using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using LANCommander.Server.Settings.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.UI.Tests.Components;

/// <summary>
/// Export coverage running against the fixture's real (SQLite) provider rather than the
/// in-memory provider, so Include/AsSplitQuery behave the way they do on a live server.
/// </summary>
[Collection("BUnit")]
public class ExportContextTests
{
    private readonly BUnitServerFixture _fixture;

    public ExportContextTests(BUnitServerFixture fixture) => _fixture = fixture;

    /// <summary>Writes a real (tiny) zip so archive size recalculation can read it.</summary>
    private static async Task WriteArchiveFileAsync(string path, string content)
    {
        await using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        var entry = zip.CreateEntry("content.txt");

        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream);
        await writer.WriteAsync(content);
    }

    private string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(path);
        return path;
    }

    [Fact]
    public async Task RedistributableExportContainsEveryArchive()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var redistributableService = scope.ServiceProvider.GetRequiredService<RedistributableService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"Redist {Guid.NewGuid():N}",
        });

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                RedistributableId = redistributable.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"1.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await WriteArchiveFileAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"archive-{i}");
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();

        var items = (await exportContext.InitializeExportAsync(redistributable.Id, ImportExportRecordType.Redistributable)).ToList();

        Assert.Equal(3, items.Count(i => i.Type == ImportExportRecordType.Archive));

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "export.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        using var zip = ZipFile.OpenRead(exportPath);

        var archiveEntries = zip.Entries.Where(e => e.FullName.StartsWith("Archives/")).ToList();

        string yaml;
        using (var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open()))
            yaml = await reader.ReadToEndAsync();

        var manifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Redistributable>(yaml);

        Assert.Equal(3, archiveEntries.Count);
        Assert.Equal(3, manifest.Archives.Count);
    }

    [Fact]
    public async Task RedistributableRoundTripKeepsEveryArchive()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var redistributableService = scope.ServiceProvider.GetRequiredService<RedistributableService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"RoundTrip {Guid.NewGuid():N}",
        });

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                RedistributableId = redistributable.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"2.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await WriteArchiveFileAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"round-trip-{i}");
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(redistributable.Id, ImportExportRecordType.Redistributable)).ToList();

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "roundtrip.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        // Remove the source records so the import takes the "add" path, like importing onto a
        // different server would.
        foreach (var archive in await archiveService.GetAsync(a => a.RedistributableId == redistributable.Id))
            await archiveService.DeleteAsync(archive);

        await redistributableService.DeleteAsync(await redistributableService.GetAsync(redistributable.Id));

        var importContext = scope.ServiceProvider.GetRequiredService<ImportContextFactory>().Create();
        var importService = _fixture.Factory.RealServices.GetRequiredService<ImportService>();

        importService.AddContext(importContext);

        var importItems = (await importContext.InitializeImportAsync(exportPath)).ToList();

        await importContext.PrepareImportQueueAsync(importItems.Select(i => Guid.Empty), storageLocation.Id);
        await importContext.ImportQueueAsync();

        var imported = await archiveService.GetAsync(a => a.RedistributableId == redistributable.Id);

        Assert.Equal(3, imported.Count);
    }

    [Fact]
    public async Task ToolExportContainsEveryArchive()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var toolService = scope.ServiceProvider.GetRequiredService<ToolService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var tool = await toolService.AddAsync(new Tool { Name = $"Tool {Guid.NewGuid():N}" });

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                ToolId = tool.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"3.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await WriteArchiveFileAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"tool-{i}");
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(tool.Id, ImportExportRecordType.Tool)).ToList();

        Assert.Equal(3, items.Count(i => i.Type == ImportExportRecordType.Archive));

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "tool.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        using var zip = ZipFile.OpenRead(exportPath);

        string yaml;
        using (var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open()))
            yaml = await reader.ReadToEndAsync();

        var manifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Tool>(yaml);

        Assert.Equal(3, zip.Entries.Count(e => e.FullName.StartsWith("Archives/")));
        Assert.Equal(3, manifest.Archives.Count);
    }

    [Fact]
    public async Task GameExportContainsEveryArchiveExactlyOnce()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var game = await gameService.AddAsync(new Game { Title = $"Game {Guid.NewGuid():N}" });

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                GameId = game.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"4.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await WriteArchiveFileAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"game-{i}");
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(game.Id, ImportExportRecordType.Game)).ToList();

        Assert.Equal(3, items.Count(i => i.Type == ImportExportRecordType.Archive));

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "game.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        using var zip = ZipFile.OpenRead(exportPath);

        string yaml;
        using (var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open()))
            yaml = await reader.ReadToEndAsync();

        var manifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Game>(yaml);

        Assert.Equal(3, zip.Entries.Count(e => e.FullName.StartsWith("Archives/")));
        Assert.Equal(3, manifest.Archives.Count);
    }

    [Fact]
    public async Task RedistributableReimportOverExistingKeepsEveryArchive()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var redistributableService = scope.ServiceProvider.GetRequiredService<RedistributableService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"Reimport {Guid.NewGuid():N}",
        });

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                RedistributableId = redistributable.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"5.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await WriteArchiveFileAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"reimport-{i}");
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(redistributable.Id, ImportExportRecordType.Redistributable)).ToList();

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "reimport.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        // Import on top of the records that are already there.
        var importContext = scope.ServiceProvider.GetRequiredService<ImportContextFactory>().Create();
        _fixture.Factory.RealServices.GetRequiredService<ImportService>().AddContext(importContext);

        var importItems = (await importContext.InitializeImportAsync(exportPath)).ToList();

        await importContext.PrepareImportQueueAsync(importItems.Select(i => Guid.Empty), storageLocation.Id);
        await importContext.ImportQueueAsync();

        var after = await archiveService.GetAsync(a => a.RedistributableId == redistributable.Id);

        Assert.Equal(3, after.Count);
    }

    [Fact]
    public async Task ArchivesBeingReadElsewhereStillExport()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var redistributableService = scope.ServiceProvider.GetRequiredService<RedistributableService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"Locked {Guid.NewGuid():N}",
        });

        var paths = new List<string>();

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                RedistributableId = redistributable.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"6.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            var path = Path.Combine(storageLocation.Path, archive.ObjectKey);
            await WriteArchiveFileAsync(path, $"locked-{i}");
            paths.Add(path);
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(redistributable.Id, ImportExportRecordType.Redistributable)).ToList();

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "locked.lcx");

        // Two of the three archives are being served to a launcher at the same time. The download
        // endpoint opens them exactly like this.
        using (new FileStream(paths[0], FileMode.Open, FileAccess.Read, FileShare.Read))
        using (new FileStream(paths[1], FileMode.Open, FileAccess.Read, FileShare.Read))
        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        using var zip = ZipFile.OpenRead(exportPath);

        string yaml;
        using (var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open()))
            yaml = await reader.ReadToEndAsync();

        var manifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Redistributable>(yaml);

        Assert.Empty(exportContext.Errored);
        Assert.Equal(3, zip.Entries.Count(e => e.FullName.StartsWith("Archives/")));
        Assert.Equal(3, manifest.Archives.Count);
        Assert.DoesNotContain(manifest.Archives, a => a == null);
    }

    [Fact]
    public async Task ToolExportContainsActions()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var toolService = scope.ServiceProvider.GetRequiredService<ToolService>();
        var actionService = scope.ServiceProvider.GetRequiredService<ActionService>();

        var tool = await toolService.AddAsync(new Tool { Name = $"Tool {Guid.NewGuid():N}" });

        await actionService.AddAsync(new Data.Models.Action
        {
            ToolId = tool.Id,
            Name = "Run",
            Path = "tool.exe",
            PrimaryAction = true,
        });

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(tool.Id, ImportExportRecordType.Tool)).ToList();

        Assert.Single(items, i => i.Type == ImportExportRecordType.Action);

        await exportContext.PrepareExportQueueAsync(items.Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "tool-actions.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        using var zip = ZipFile.OpenRead(exportPath);

        string yaml;
        using (var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open()))
            yaml = await reader.ReadToEndAsync();

        var manifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Tool>(yaml);

        Assert.Single(manifest.Actions);
        Assert.Equal("Run", manifest.Actions.First().Name);
    }

    [Fact]
    public async Task DeselectedGameArchivesAreLeftOutOfTheExport()
    {
        using var scope = _fixture.Factory.RealServices.CreateScope();

        var storageLocationService = scope.ServiceProvider.GetRequiredService<StorageLocationService>();
        var gameService = scope.ServiceProvider.GetRequiredService<GameService>();
        var archiveService = scope.ServiceProvider.GetRequiredService<ArchiveService>();

        var storageLocation = (await storageLocationService.GetAsync(l => l.Type == StorageLocationType.Archive)).First();

        var game = await gameService.AddAsync(new Game { Title = $"Partial {Guid.NewGuid():N}" });

        for (var i = 0; i < 3; i++)
        {
            var archive = await archiveService.AddAsync(new Archive
            {
                GameId = game.Id,
                ObjectKey = Guid.NewGuid().ToString(),
                Version = $"7.0.{i}",
                StorageLocationId = storageLocation.Id,
            });

            await WriteArchiveFileAsync(Path.Combine(storageLocation.Path, archive.ObjectKey), $"partial-{i}");
        }

        var exportContext = scope.ServiceProvider.GetRequiredService<ExportContextFactory>().Create();
        var items = (await exportContext.InitializeExportAsync(game.Id, ImportExportRecordType.Game)).ToList();

        var archiveItems = items.Where(i => i.Type == ImportExportRecordType.Archive).ToList();
        var dropped = archiveItems.First();

        await exportContext.PrepareExportQueueAsync(items.Where(i => i.Id != dropped.Id).Select(i => i.Id));

        var exportPath = Path.Combine(TempDirectory(), "partial.lcx");

        await using (var fs = new FileStream(exportPath, FileMode.Create, FileAccess.Write))
            await exportContext.ExportQueueAsync(fs);

        using var zip = ZipFile.OpenRead(exportPath);

        string yaml;
        using (var reader = new StreamReader(zip.GetEntry(ManifestHelper.ManifestFilename)!.Open()))
            yaml = await reader.ReadToEndAsync();

        var manifest = ManifestHelper.Deserialize<SDK.Models.Manifest.Game>(yaml);

        Assert.Equal(2, manifest.Archives.Count);
        Assert.DoesNotContain(manifest.Archives, a => a.Id == dropped.Id);
        Assert.Equal(2, zip.Entries.Count(e => e.FullName.StartsWith("Archives/")));
    }
}
