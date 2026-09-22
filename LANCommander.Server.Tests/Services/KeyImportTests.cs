using System.IO.Compression;
using System.Text;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.Server.Data;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using DataKey = LANCommander.Server.Data.Models.Key;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;
using ManifestKey = LANCommander.SDK.Models.Manifest.Key;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Covers what happens to a game's CD keys when its package is imported. This is the other half
/// of the "keys got duplicated" report: importing or re-importing a game rewrites the whole
/// game, keys included.
/// </summary>
[Collection("Application")]
public class KeyImportTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private static ManifestGame ManifestWithKeys(Guid id, params string[] values) => new()
    {
        Id = id,
        Title = $"Key Import {id:N}",
        SortTitle = $"Key Import {id:N}",
        DirectoryName = "KeyImport",
        Type = GameType.MainGame,
        Keys = values.Select(v => new ManifestKey { Value = v }).ToList(),
    };

    private async Task<List<DataKey>> LoadKeysAsync(Guid gameId)
    {
        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        return await context.Set<DataKey>()
            .AsNoTracking()
            .Where(k => k.GameId == gameId)
            .ToListAsync();
    }

    /// <summary>
    /// Runs the real package import path: a .lcx is nothing but a zip with a manifest at its
    /// root, so writing one on the fly exercises the same code an uploaded export goes through.
    /// </summary>
    private async Task ImportAsync(ManifestGame manifest)
    {
        var storageLocationService = GetService<StorageLocationService>();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        if (storageLocation == null)
        {
            await EnsureStorageLocationsExistAsync();

            storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);
        }

        var packagePath = Path.Combine(GetTemporaryDirectory(), $"{manifest.Id:N}.lcx");

        using (var file = File.Create(packagePath))
        using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry(ManifestHelper.ManifestFilename);

            await using var stream = entry.Open();
            await using var writer = new StreamWriter(stream, Encoding.UTF8);

            await writer.WriteAsync(ManifestHelper.Serialize(manifest));
        }

        var importContext = GetService<ImportContextFactory>().Create();

        // The queue swallows importer failures and only surfaces them as a deadlock once nothing
        // is left that can make progress, so collect them to get a usable assertion message.
        var errors = new List<string>();

        importContext.OnImportError.EventRaised += update =>
        {
            errors.Add($"{update.CurrentItem?.Type} {update.CurrentItem?.Name}: {update.Error}");
            return Task.CompletedTask;
        };

        await importContext.InitializeImportAsync(packagePath);
        await importContext.PrepareImportQueueAsync([], storageLocation!.Id);
        await importContext.ImportQueueAsync();

        importContext.Dispose();

        errors.ShouldBeEmpty();
    }

    /// <summary>
    /// An imported key is worthless without its value. If the importer drops it, every row on the
    /// game reads the same (empty) key, which is exactly the "one key repeating for all entries"
    /// symptom.
    /// </summary>
    [Fact]
    public async Task ImportedKeysKeepTheirValues()
    {
        var gameId = Guid.NewGuid();

        await ImportAsync(ManifestWithKeys(gameId, "AAAA-1111", "BBBB-2222", "CCCC-3333"));

        var keys = await LoadKeysAsync(gameId);

        keys.Count.ShouldBe(3);
        keys.Select(k => k.Value).OrderBy(v => v).ShouldBe(["AAAA-1111", "BBBB-2222", "CCCC-3333"]);
    }

    /// <summary>
    /// Re-importing the same package is an update, not a second copy. If the importer cannot
    /// recognise the keys it already wrote, every re-import doubles the game's key pool.
    /// </summary>
    [Fact]
    public async Task ReImportingAGameDoesNotDuplicateItsKeys()
    {
        var gameId = Guid.NewGuid();

        await ImportAsync(ManifestWithKeys(gameId, "DDDD-1111", "EEEE-2222"));
        await ImportAsync(ManifestWithKeys(gameId, "DDDD-1111", "EEEE-2222"));

        var keys = await LoadKeysAsync(gameId);

        keys.Count.ShouldBe(2);
        keys.Select(k => k.Value).OrderBy(v => v).ShouldBe(["DDDD-1111", "EEEE-2222"]);
    }

    /// <summary>
    /// A package that adds a key on top of the ones already imported should leave the existing
    /// rows alone and add exactly one.
    /// </summary>
    [Fact]
    public async Task ReImportingWithAnExtraKeyAddsOnlyThatKey()
    {
        var gameId = Guid.NewGuid();

        await ImportAsync(ManifestWithKeys(gameId, "FFFF-1111"));

        var first = await LoadKeysAsync(gameId);
        first.Count.ShouldBe(1);

        await ImportAsync(ManifestWithKeys(gameId, "FFFF-1111", "GGGG-2222"));

        var keys = await LoadKeysAsync(gameId);

        keys.Count.ShouldBe(2);
        keys.Select(k => k.Value).OrderBy(v => v).ShouldBe(["FFFF-1111", "GGGG-2222"]);
        keys.ShouldContain(k => k.Id == first[0].Id);
    }

    /// <summary>
    /// Two games can legitimately ship the same key value. Importing one must not reach across
    /// and edit or steal the other game's row.
    /// </summary>
    [Fact]
    public async Task ImportingAKeyDoesNotTouchAnotherGamesIdenticalKey()
    {
        var firstGameId = Guid.NewGuid();
        var secondGameId = Guid.NewGuid();

        await ImportAsync(ManifestWithKeys(firstGameId, "SHARED-VALUE"));
        await ImportAsync(ManifestWithKeys(secondGameId, "SHARED-VALUE"));

        (await LoadKeysAsync(firstGameId)).Count.ShouldBe(1);
        (await LoadKeysAsync(secondGameId)).Count.ShouldBe(1);
    }
}
