using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.ImportExport;
using LANCommander.Server.ImportExport.Factories;
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
