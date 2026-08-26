using System.IO.Compression;
using System.Net;
using System.Text;
using LANCommander.SDK;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Options;
using Shouldly;

namespace LANCommander.Server.Tests.Endpoints;

/// <summary>
/// Covers the MEDIUM "pinned archive downloads bypass Server.Archives.AllowInsecureDownloads"
/// finding.
///
/// Normal generated install plans always pin an ArchiveId, and the SDK used to stream a pinned
/// archive from the raw, ungated <c>/Download/Archive/{id}</c> route instead of the game-scoped
/// <c>/api/Games/{id}/Download</c> one — so the download policy gate (and the archive-belongs-to-
/// this-game validation) effectively applied to almost no real download at all.
///
/// The fix is two-sided and both sides are asserted here:
/// - the SDK now downloads game archives through <c>/api/Games/{gameId}/Download?archiveId=...</c>,
///   which enforces the gate and rejects an archive that isn't the game's;
/// - the raw route now applies the same gate to <em>game</em> archives, so the bypass is closed for
///   any client, while non-game (redistributable) archives keep their existing public behavior.
///
/// <see cref="ApplicationFixture.HttpClient"/> carries no bearer token (the SDK clients attach it
/// per-request), so requests made through it directly are anonymous — which is exactly what these
/// gate assertions need. AllowInsecureDownloads is false by default.
/// </summary>
[Collection("Application")]
public class ArchiveDownloadGateTests(ApplicationFixture fixture) : BaseTest(fixture)
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

    private async Task<(Game Game, Archive Older, Archive Newer)> CreateGameWithTwoArchivesAsync(string titlePrefix)
    {
        var gameService = GetService<GameService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var game = await gameService.AddAsync(new Game { Title = $"{titlePrefix} {Guid.NewGuid():N}" });

        var older = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        var newer = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            Version = "2.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        older.CreatedOn = DateTime.UtcNow.AddDays(-2);
        newer.CreatedOn = DateTime.UtcNow.AddDays(-1);

        older = await archiveService.UpdateAsync(older);
        newer = await archiveService.UpdateAsync(newer);

        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, older.ObjectKey),
            new Dictionary<string, string> { ["marker.txt"] = "old-content-v1" });

        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, newer.ObjectKey),
            new Dictionary<string, string> { ["marker.txt"] = "new-content-v2" });

        return (game, older, newer);
    }

    [Fact]
    public async Task AllowInsecureDownloadsIsOffByDefault()
    {
        // The gate assertions below only mean anything while the policy is actually restrictive.
        var settings = GetService<IOptions<Settings.Settings>>();

        settings.Value.Server.Archives.AllowInsecureDownloads.ShouldBeFalse();
    }

    [Fact]
    public async Task PinnedGameArchiveDownloadIsGatedOnTheGameEndpoint()
    {
        var (game, older, _) = await CreateGameWithTwoArchivesAsync("Download Gate Test Game");

        // This is the exact route the SDK now uses for a pinned (plan-generated) download.
        var response = await ApplicationFixture.Instance.HttpClient.GetAsync(
            $"/api/Games/{game.Id}/Download?archiveId={older.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RawArchiveRouteRejectsAnonymousGameArchiveDownloads()
    {
        // Closing the bypass itself: even a client that goes straight at the raw route must be
        // subject to the same policy for a game archive.
        var (_, older, _) = await CreateGameWithTwoArchivesAsync("Raw Route Gate Test Game");

        var response = await ApplicationFixture.Instance.HttpClient.GetAsync($"/Download/Archive/{older.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RawArchiveRouteStillServesNonGameArchivesAnonymously()
    {
        // Redistributable archives are shared payloads with no per-game policy attached and are
        // fetched by id without any game context — hardening game archives must not break them.
        var redistributableService = GetService<RedistributableService>();
        var archiveService = GetService<ArchiveService>();
        var storageLocationService = GetService<StorageLocationService>();

        await EnsureStorageLocationsExistAsync();

        var storageLocation = await storageLocationService.DefaultAsync(StorageLocationType.Archive);

        var redistributable = await redistributableService.AddAsync(new Redistributable
        {
            Name = $"Redist {Guid.NewGuid():N}",
        });

        var archive = await archiveService.AddAsync(new Archive
        {
            RedistributableId = redistributable.Id,
            Version = "1.0",
            ObjectKey = Guid.NewGuid().ToString(),
            StorageLocationId = storageLocation.Id,
        });

        WriteZip(AppPaths.ResolveStorageLocationPath(storageLocation.Path, archive.ObjectKey),
            new Dictionary<string, string> { ["marker.txt"] = "redist-content" });

        var response = await ApplicationFixture.Instance.HttpClient.GetAsync($"/Download/Archive/{archive.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PinnedGameArchiveDownloadServesTheExactArchiveThroughTheGatedEndpoint()
    {
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (game, older, newer) = await CreateGameWithTwoArchivesAsync("Pinned Download Test Game");

        var destination = GetTemporaryDirectory();

        try
        {
            // ApplyUpdateArchiveAsync is the public entry point that downloads one exact archive;
            // it goes through the same StreamArchiveAsync path a pinned plan item uses.
            (await GameClient.ApplyUpdateArchiveAsync(older.Id, game.Id, destination)).ShouldBeTrue();

            var extracted = Path.Combine(destination, "marker.txt");

            File.Exists(extracted).ShouldBeTrue();
            (await File.ReadAllTextAsync(extracted)).ShouldBe("old-content-v1", "the pinned archive must be served, not the game's newest/effective default");
        }
        finally
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
        }

        newer.Id.ShouldNotBe(older.Id);
    }

    [Fact]
    public async Task PinnedGameArchiveDownloadRejectsAnArchiveThatBelongsToAnotherGame()
    {
        // Ownership validation is only performed by the game-scoped endpoint — the raw route
        // serves any archive by id. A cross-game archive id being refused is therefore direct
        // evidence that pinned downloads really do go through the gated, validating endpoint.
        await EnsureAdminUserCreatedAsync();
        await AuthenticateAsync(TestConstants.AdminUserName, TestConstants.AdminInitialPassword);

        var (gameA, _, _) = await CreateGameWithTwoArchivesAsync("Ownership Test Game A");
        var (_, foreignArchive, _) = await CreateGameWithTwoArchivesAsync("Ownership Test Game B");

        var destination = GetTemporaryDirectory();

        try
        {
            var ex = await Should.ThrowAsync<HttpRequestException>(async () =>
                await GameClient.ApplyUpdateArchiveAsync(foreignArchive.Id, gameA.Id, destination));

            ex.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

            // Nothing from the other game's archive may have been written.
            File.Exists(Path.Combine(destination, "marker.txt")).ShouldBeFalse();
        }
        finally
        {
            if (Directory.Exists(destination))
                Directory.Delete(destination, true);
        }
    }
}
