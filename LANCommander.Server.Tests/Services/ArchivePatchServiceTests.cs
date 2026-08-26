using System.IO.Compression;
using System.Text;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

[Collection("Application")]
public class ArchivePatchServiceTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    private static void WriteZip(string path, IDictionary<string, string> entries)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

        foreach (var (name, content) in entries)
        {
            var entry = zip.CreateEntry(name);

            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, Encoding.UTF8);

            writer.Write(content);
        }
    }

    private async Task<(Game Game, Archive From, Archive To, StorageLocation StorageLocation)> CreateFromAndToArchivesAsync(
        IDictionary<string, string> fromEntries,
        IDictionary<string, string> toEntries)
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = "Patch Test Game " + Guid.NewGuid().ToString("N") });

        var from = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        var to = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "2.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, from.ObjectKey), fromEntries);
        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, to.ObjectKey), toEntries);

        return (game, from, to, storageLocation);
    }

    private static IEnumerable<string> ReadZipEntryNames(string path)
    {
        using var zip = ZipFile.OpenRead(path);

        return zip.Entries.Select(e => e.FullName).ToList();
    }

    [Fact]
    public async Task GeneratePatchOnlyIncludesChangedOrNewEntries()
    {
        var archivePatchService = GetService<ArchivePatchService>();
        var storageLocationService = GetService<StorageLocationService>();

        var (_, from, to, storageLocation) = await CreateFromAndToArchivesAsync(
            fromEntries: new Dictionary<string, string>
            {
                ["a.txt"] = "unchanged content",
                ["b.txt"] = "old content",
            },
            toEntries: new Dictionary<string, string>
            {
                ["a.txt"] = "unchanged content", // same CRC -> excluded from patch
                ["b.txt"] = "new content", // changed -> included
                ["c.txt"] = "brand new file", // new -> included
            });

        var fromPath = AppPaths.ResolveStorageLocationPath(storageLocation.Path, from.ObjectKey);
        var toPath = AppPaths.ResolveStorageLocationPath(storageLocation.Path, to.ObjectKey);

        var fromBytesBefore = await File.ReadAllBytesAsync(fromPath);
        var toBytesBefore = await File.ReadAllBytesAsync(toPath);

        var patch = await archivePatchService.GeneratePatchAsync(from.Id, to.Id);

        patch.FromArchiveId.ShouldBe(from.Id);
        patch.ToArchiveId.ShouldBe(to.Id);
        patch.StorageLocationId.ShouldBe(storageLocation.Id);
        patch.CompressedSize.ShouldBeGreaterThan(0);
        patch.UncompressedSize.ShouldBeGreaterThan(0);

        var patchPath = await archivePatchService.GetPatchFileLocationAsync(patch);

        File.Exists(patchPath).ShouldBeTrue();

        var patchEntries = ReadZipEntryNames(patchPath).ToList();

        patchEntries.ShouldContain("b.txt");
        patchEntries.ShouldContain("c.txt");
        patchEntries.ShouldNotContain("a.txt");
        patchEntries.Count.ShouldBe(2);

        // Neither source archive is modified by patch generation.
        (await File.ReadAllBytesAsync(fromPath)).ShouldBe(fromBytesBefore);
        (await File.ReadAllBytesAsync(toPath)).ShouldBe(toBytesBefore);
    }

    [Fact]
    public async Task GeneratePatchRejectsIdenticalFromAndToArchive()
    {
        var archivePatchService = GetService<ArchivePatchService>();

        var (_, from, _, _) = await CreateFromAndToArchivesAsync(
            fromEntries: new Dictionary<string, string> { ["a.txt"] = "content" },
            toEntries: new Dictionary<string, string> { ["a.txt"] = "content" });

        await Should.ThrowAsync<ArgumentException>(
            async () => await archivePatchService.GeneratePatchAsync(from.Id, from.Id));
    }

    [Fact]
    public async Task DeletingArchiveRemovesAssociatedPatchRowAndFile()
    {
        var archivePatchService = GetService<ArchivePatchService>();
        var archiveService = GetService<ArchiveService>();
        var contextFactory = ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();

        var (_, from, to, _) = await CreateFromAndToArchivesAsync(
            fromEntries: new Dictionary<string, string> { ["a.txt"] = "content" },
            toEntries: new Dictionary<string, string> { ["a.txt"] = "changed content" });

        var patch = await archivePatchService.GeneratePatchAsync(from.Id, to.Id);
        var patchPath = await archivePatchService.GetPatchFileLocationAsync(patch);

        File.Exists(patchPath).ShouldBeTrue();

        // Deleting either endpoint archive must clean up patches that reference it, since a
        // patch is meaningless without both of the full archives it links.
        await archiveService.DeleteAsync(to);

        File.Exists(patchPath).ShouldBeFalse();

        await using var context = await contextFactory.CreateDbContextAsync();

        var remaining = await context.Set<ArchivePatch>().FirstOrDefaultAsync(p => p.Id == patch.Id);

        remaining.ShouldBeNull();
    }
}
