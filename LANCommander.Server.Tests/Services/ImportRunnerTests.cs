using System.IO.Compression;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.ImportExport.Services;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Regression coverage for the import commit path.
/// <para>
/// The API import endpoints used to call only <c>InitializeImportAsync</c>, so uploading a
/// package produced an orphaned archive row and never created a record. These tests assert
/// that a full run actually lands the game in the database.
/// </para>
/// </summary>
[Collection("Application")]
public class ImportRunnerTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private const string FixtureFileName = "lotrbfme2.lcx";

    [Fact]
    public async Task ImportingAnUploadedPackageCreatesTheGame()
    {
        var gameService = GetService<GameService>();
        var storageLocationService = GetService<StorageLocationService>();
        var importContext = GetService<ImportContextFactory>().Create();

        var (objectKey, storageLocation) = await StageUploadedPackageAsync();

        var path = Path.Combine(storageLocation.Path, objectKey.ToString());

        var items = (await importContext.InitializeImportAsync(path)).ToList();

        items.ShouldNotBeEmpty();

        await importContext.PrepareImportQueueAsync([], storageLocation.Id);
        await importContext.ImportQueueAsync();

        importContext.Processed.ShouldBeGreaterThan(0);

        // The importers key entities off the manifest id, so the created game is addressable by
        // the id the package declared.
        var manifest = importContext.Manifest.ShouldBeOfType<SDK.Models.Manifest.Game>();

        var game = await gameService.GetAsync(manifest.Id);

        game.ShouldNotBeNull();
        game.Title.ShouldNotBeNullOrWhiteSpace();

        importContext.Dispose();
    }

    [Fact]
    public async Task ImportingAnUnknownObjectKeyThrows()
    {
        var importContext = GetService<ImportContextFactory>().Create();

        await Should.ThrowAsync<Exception>(() =>
            importContext.InitializeImportAsync(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.lcx")));

        importContext.Dispose();
    }

    [Fact]
    public async Task ImportRunnerImportsAStream()
    {
        await EnsureStorageLocationsExistAsync();

        var importRunner = GetService<ImportRunner>();
        var gameId = Guid.NewGuid();
        await using var package = new MemoryStream();

        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(ManifestHelper.ManifestFilename);
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream, Encoding.UTF8);

            await writer.WriteAsync(ManifestHelper.Serialize(new SDK.Models.Manifest.Game
            {
                Id = gameId,
                Title = "Plugin Package Import",
                Version = "1.0",
                DirectoryName = "PluginPackageImport",
            }));
        }

        package.Position = 0;

        var result = await importRunner.RunStreamAsync(
            package,
            manifestType: ManifestType.Game);

        result.ManifestType.ShouldBe(ManifestType.Game);
        result.RecordId.ShouldBe(gameId);
        result.ImportedCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ImportRunnerReturnsExistingGameIdWhenMatchedByTitle()
    {
        await EnsureStorageLocationsExistAsync();

        var gameService = GetService<GameService>();
        var importRunner = GetService<ImportRunner>();
        var title = $"Plugin Package Existing Game {Guid.NewGuid()}";
        var existing = await gameService.AddAsync(new Game
        {
            Id = Guid.NewGuid(),
            Title = title,
        });
        await using var package = new MemoryStream();

        using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(ManifestHelper.ManifestFilename);
            await using var entryStream = entry.Open();
            await using var writer = new StreamWriter(entryStream, Encoding.UTF8);

            await writer.WriteAsync(ManifestHelper.Serialize(new SDK.Models.Manifest.Game
            {
                Id = Guid.NewGuid(),
                Title = title,
                Version = "1.0",
                DirectoryName = "PluginPackageExistingGame",
            }));
        }

        package.Position = 0;

        var result = await importRunner.RunStreamAsync(
            package,
            manifestType: ManifestType.Game);

        result.RecordId.ShouldBe(existing.Id);
        result.ImportedCount.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ImportRunnerRejectsOversizedStreams()
    {
        var importRunner = GetService<ImportRunner>();
        await using var package = new MemoryStream(new byte[16]);

        await Should.ThrowAsync<InvalidDataException>(() =>
            importRunner.RunStreamAsync(package, maxPackageBytes: 8));
    }

    [Fact]
    public async Task ImportRunnerHonorsStreamCancellation()
    {
        var importRunner = GetService<ImportRunner>();
        await using var package = new MemoryStream(new byte[16]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(() =>
            importRunner.RunStreamAsync(
                package,
                cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// Mimics what the chunked upload endpoints leave behind: the package sitting in the
    /// default archive storage location under an object key, with a matching Archive row.
    /// </summary>
    private async Task<(Guid ObjectKey, StorageLocation StorageLocation)> StageUploadedPackageAsync()
    {
        var storageLocationService = GetService<StorageLocationService>();
        var archiveService = GetService<ArchiveService>();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        if (storageLocation == null)
        {
            await EnsureStorageLocationsExistAsync();

            storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);
        }

        storageLocation.ShouldNotBeNull();

        var objectKey = Guid.NewGuid();
        var sourcePath = Path.Combine(AppContext.BaseDirectory, "Files", FixtureFileName);

        File.Exists(sourcePath).ShouldBeTrue($"Test fixture '{sourcePath}' is missing.");

        Directory.CreateDirectory(storageLocation.Path);

        File.Copy(sourcePath, Path.Combine(storageLocation.Path, objectKey.ToString()), overwrite: true);

        await archiveService.AddAsync(new Archive
        {
            ObjectKey = objectKey.ToString(),
            Version = "1.0",
            StorageLocationId = storageLocation.Id,
        });

        return (objectKey, storageLocation);
    }
}
