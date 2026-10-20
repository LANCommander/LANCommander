using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LANCommander.Server.Tests.Data;

/// <summary>
/// Archives uploaded after the startup backfill were never linked to a version, so updates and the
/// launcher's version picker never saw them; and the update check compared version labels, so a
/// rolled-back install always reported an update.
/// </summary>
[Collection("Application")]
public class VersionLinkingTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private async Task<Archive> AddArchiveAsync(Guid gameId, string version)
    {
        var storageLocation = await GetStorageLocationAsync();

        return await GetService<ArchiveService>().AddAsync(new Archive
        {
            GameId = gameId,
            ObjectKey = Unique("object"),
            Version = version,
            StorageLocationId = storageLocation.Id,
        });
    }

    private async Task<GameVersion> GetVersionOfArchiveAsync(Guid archiveId)
    {
        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        var archive = await context.Set<Archive>().AsNoTracking().FirstAsync(a => a.Id == archiveId);

        archive.GameVersionId.ShouldNotBeNull();

        return await context.GameVersions.AsNoTracking().FirstAsync(v => v.Id == archive.GameVersionId);
    }

    [Fact]
    public async Task LinkArchiveUsesExplicitTargetVersion()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        var target = await versionService.CreateAsync(game.Id, "1.0");
        await versionService.CreateAsync(game.Id, "2.0");

        var archive = await AddArchiveAsync(game.Id, "something else");

        await versionService.LinkArchiveAsync(archive.Id, target.Id);

        (await GetVersionOfArchiveAsync(archive.Id)).Id.ShouldBe(target.Id);
    }

    [Fact]
    public async Task LinkArchiveMatchesVersionString()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        var first = await versionService.CreateAsync(game.Id, "1.0");
        await versionService.CreateAsync(game.Id, "2.0");

        var archive = await AddArchiveAsync(game.Id, "1.0");

        await versionService.LinkArchiveAsync(archive.Id);

        (await GetVersionOfArchiveAsync(archive.Id)).Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task LinkArchiveFillsEmptyPlaceholderVersion()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        var placeholder = await versionService.GetOrCreateLatestAsync(game.Id);

        var archive = await AddArchiveAsync(game.Id, "1.0");

        await versionService.LinkArchiveAsync(archive.Id);

        var version = await GetVersionOfArchiveAsync(archive.Id);

        version.Id.ShouldBe(placeholder.Id);
        version.Version.ShouldBe("1.0");
        (await versionService.GetAllAsync(game.Id)).Count().ShouldBe(1);
    }

    [Fact]
    public async Task LinkArchiveCreatesNewVersionWithCopiedConfig()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        var first = await versionService.CreateAsync(game.Id, "1.0");
        await versionService.LinkArchiveAsync((await AddArchiveAsync(game.Id, "1.0")).Id, first.Id);
        await AddScriptAsync(game.Id);

        var archive = await AddArchiveAsync(game.Id, "2.0");

        await versionService.LinkArchiveAsync(archive.Id, changelog: "Fixes");

        var version = await GetVersionOfArchiveAsync(archive.Id);

        version.Id.ShouldNotBe(first.Id);
        version.Version.ShouldBe("2.0");
        version.Changelog.ShouldBe("Fixes");
        version.SortOrder.ShouldBeGreaterThan(first.SortOrder);

        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        (await context.Set<Script>().CountAsync(s => s.GameVersionId == version.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task LinkArchiveToVersionWithArchiveUnlinksThePreviousOne()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        var version = await versionService.CreateAsync(game.Id, "1.0");
        var original = await AddArchiveAsync(game.Id, "1.0");
        await versionService.LinkArchiveAsync(original.Id, version.Id);

        var replacement = await AddArchiveAsync(game.Id, "1.0");
        await versionService.LinkArchiveAsync(replacement.Id, version.Id);

        (await GetVersionOfArchiveAsync(replacement.Id)).Id.ShouldBe(version.Id);

        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        (await context.Set<Archive>().AsNoTracking().FirstAsync(a => a.Id == original.Id)).GameVersionId.ShouldBeNull();
    }

    /// <summary>Seeds versions 1.0, 2.0 and 3.0, each with an archive.</summary>
    private async Task<(Game Game, List<GameVersion> Versions)> SeedLinkedVersionsAsync()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        var versions = new List<GameVersion>();

        foreach (var label in new[] { "1.0", "2.0", "3.0" })
        {
            var version = await versionService.CreateAsync(game.Id, label);

            await versionService.LinkArchiveAsync((await AddArchiveAsync(game.Id, label)).Id, version.Id);

            versions.Add(version);
        }

        // Creating each version published the one before; publish the newest too
        await versionService.SetPublishedAsync(versions[^1].Id, true);

        return (game, versions);
    }

    [Fact]
    public async Task HasUpdateIsFalseOnLatestVersion()
    {
        var (game, versions) = await SeedLinkedVersionsAsync();

        (await GetService<GameService>().HasUpdateAsync(game.Id, versions[2].Id, "3.0")).ShouldBeFalse();
    }

    [Fact]
    public async Task HasUpdateIsTrueOnOlderVersion()
    {
        var (game, versions) = await SeedLinkedVersionsAsync();

        (await GetService<GameService>().HasUpdateAsync(game.Id, versions[0].Id, "1.0")).ShouldBeTrue();
    }

    [Fact]
    public async Task HasUpdateCountsNewerConfigOnlyVersions()
    {
        var (game, versions) = await SeedLinkedVersionsAsync();

        var v4 = await GetService<GameVersionService>().CreateAsync(game.Id, "4.0");

        // A draft isn't offered, so it isn't an update until it's published
        (await GetService<GameService>().HasUpdateAsync(game.Id, versions[2].Id, "3.0")).ShouldBeFalse();

        await GetService<GameVersionService>().SetPublishedAsync(v4.Id, true);

        (await GetService<GameService>().HasUpdateAsync(game.Id, versions[2].Id, "3.0")).ShouldBeTrue();
    }

    [Fact]
    public async Task HasUpdatePrefersVersionIdOverLabel()
    {
        var (game, versions) = await SeedLinkedVersionsAsync();

        // The label says latest, but the id identifies the oldest version
        (await GetService<GameService>().HasUpdateAsync(game.Id, versions[0].Id, "3.0")).ShouldBeTrue();

        // The label is stale, but the id identifies the latest version
        (await GetService<GameService>().HasUpdateAsync(game.Id, versions[2].Id, "1.0")).ShouldBeFalse();
    }

    [Fact]
    public async Task HasUpdateFallsBackToLabelWithoutVersionId()
    {
        var (game, _) = await SeedLinkedVersionsAsync();

        var gameService = GetService<GameService>();

        (await gameService.HasUpdateAsync(game.Id, null, "2.0")).ShouldBeTrue();
        (await gameService.HasUpdateAsync(game.Id, null, "3.0")).ShouldBeFalse();
        (await gameService.HasUpdateAsync(game.Id, null, "unknown")).ShouldBeTrue();
    }

    [Fact]
    public async Task GetNewerThanUsesVersionId()
    {
        var (game, versions) = await SeedLinkedVersionsAsync();

        var newer = (await GetService<GameVersionService>().GetNewerThanAsync(game.Id, versions[0].Id, null)).ToList();

        newer.Select(v => v.Version).ShouldBe(new[] { "2.0", "3.0" });
    }

    private async Task<Guid> StageUploadAsync(params string[] entries)
    {
        var storageLocation = await GetStorageLocationAsync();
        var archiveService = GetService<ArchiveService>();

        // What /api/Upload/Init leaves behind for a chunked upload
        var placeholder = await archiveService.AddAsync(new Archive
        {
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
            Version = "",
        });

        var path = await archiveService.GetArchiveFileLocationAsync(placeholder);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var entry in entries)
            {
                using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
                writer.Write(entry);
            }
        }

        return Guid.Parse(placeholder.ObjectKey);
    }

    private async Task<IResult> UploadAsync(Guid gameId, Guid objectKey, string version, string? changelog = null)
        => await GameEndpoints.UploadArchiveAsync(
            GetService<ArchiveService>(),
            GetService<GameVersionService>(),
            NullLogger<Game>.Instance,
            new SDK.Models.UploadArchiveRequest
            {
                Id = gameId,
                ObjectKey = objectKey,
                Version = version,
                Changelog = changelog,
            });

    [Fact]
    public async Task UploadArchiveFillsPlaceholderAndLinksNewVersion()
    {
        var game = await AddGameAsync();
        var objectKey = await StageUploadAsync("game.exe");

        var result = await UploadAsync(game.Id, objectKey, "1.0", "First release");

        result.ShouldBeOfType<Ok>();

        var archives = (await GetService<ArchiveService>().GetAsync(a => a.ObjectKey == objectKey.ToString())).ToList();

        archives.Count.ShouldBe(1);
        archives[0].GameId.ShouldBe(game.Id);
        archives[0].Version.ShouldBe("1.0");
        archives[0].CompressedSize.ShouldBeGreaterThan(0);

        var version = await GetVersionOfArchiveAsync(archives[0].Id);

        version.Version.ShouldBe("1.0");
        version.Changelog.ShouldBe("First release");
    }

    [Fact]
    public async Task UploadArchiveForExistingVersionReplacesTheArchive()
    {
        var game = await AddGameAsync();
        var archiveService = GetService<ArchiveService>();

        var firstKey = await StageUploadAsync("game.exe");
        (await UploadAsync(game.Id, firstKey, "1.0")).ShouldBeOfType<Ok>();

        var first = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == firstKey.ToString());
        var version = await GetVersionOfArchiveAsync(first.Id);
        var firstPath = await archiveService.GetArchiveFileLocationAsync(first);

        var secondKey = await StageUploadAsync("game.exe", "patch.dat");
        (await UploadAsync(game.Id, secondKey, "1.0")).ShouldBeOfType<Ok>();

        (await archiveService.ExistsAsync(first.Id)).ShouldBeFalse();
        File.Exists(firstPath).ShouldBeFalse();

        var second = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == secondKey.ToString());

        (await GetVersionOfArchiveAsync(second.Id)).Id.ShouldBe(version.Id);
        (await GetService<GameVersionService>().GetAllAsync(game.Id)).Count().ShouldBe(1);
    }
}
