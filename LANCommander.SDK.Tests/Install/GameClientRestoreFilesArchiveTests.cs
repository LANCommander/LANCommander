using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using LANCommander.SDK.Tests.Helpers;
using ManifestGame = LANCommander.SDK.Models.Manifest.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Regression coverage for the MEDIUM "RestoreFilesAsync repairs base files from the *latest*
/// archive" finding.
///
/// Installing/uninstalling an add-on overwrites and removes base-game files, and
/// <see cref="GameClient.RestoreFilesAsync(string, System.Guid, System.Collections.Generic.IEnumerable{string}, System.Guid?)"/>
/// puts them back. It used to do so through <c>StreamLatestArchiveAsync</c> — the game's
/// *effective default* archive — which means an installation deliberately pinned to an older/
/// different archive got repaired with files from a completely different version, silently
/// corrupting it (and bypassing the game-scoped, policy-gated download endpoint that pinned
/// downloads otherwise always use).
///
/// These tests drive a real <see cref="GameClient"/> over a recording HTTP stack, so they assert
/// the exact endpoints that were and were not requested.
/// </summary>
public class GameClientRestoreFilesArchiveTests : IDisposable
{
    private readonly string _installDirectory;

    public GameClientRestoreFilesArchiveTests()
    {
        _installDirectory = Path.Combine(Path.GetTempPath(), $"lc-restore-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(_installDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_installDirectory))
            Directory.Delete(_installDirectory, true);
    }

    private const string BaseVersion = "1.0.0";
    private const string BaseEntry = "game.exe";
    private const string AddonVersion = "2.5.0";
    private const string AddonEntry = "addon.dat";

    private static string LatestRoute(Guid gameId) => $"/api/Games/{gameId}/Download";

    private static string PinnedRoute(Guid gameId, Guid archiveId) => $"/api/Games/{gameId}/Download?archiveId={archiveId}";

    private static string ContentsRoute(Guid id, string version) => $"/api/Archives/Contents/{id}/{version}";

    private async Task<Guid> SeedBaseGameAsync(RecordingHttpMessageHandler handler, Guid? addonId = null)
    {
        var gameId = Guid.NewGuid();

        var manifest = new ManifestGame
        {
            Id = gameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Version = BaseVersion,
        };

        if (addonId.HasValue)
        {
            manifest.Addons.Add(new ManifestGame
            {
                Id = addonId.Value,
                Title = "Opposing Force",
                Type = GameType.Expansion,
                Version = AddonVersion,
            });

            // GetManifestsAsync only treats an add-on listed on the base manifest as installed
            // when its own manifest exists on disk too.
            await ManifestHelper.WriteAsync(new ManifestGame
            {
                Id = addonId.Value,
                Title = "Opposing Force",
                Type = GameType.Expansion,
                Version = AddonVersion,
            }, _installDirectory);
        }

        await ManifestHelper.WriteAsync(manifest, _installDirectory);

        // The base game's archive lists one file which is missing on disk, so ValidateFilesAsync
        // reports it as a conflict attributed to the base game itself.
        handler.MapJson(ContentsRoute(gameId, BaseVersion), new[]
        {
            new ArchiveEntry { FullName = BaseEntry, Name = BaseEntry, Crc32 = 1234u, Length = 4 },
        });

        if (addonId.HasValue)
        {
            handler.MapJson(ContentsRoute(addonId.Value, AddonVersion), new[]
            {
                new ArchiveEntry { FullName = AddonEntry, Name = AddonEntry, Crc32 = 4321u, Length = 5 },
            });
        }

        return gameId;
    }

    [Fact]
    public async Task RestoreFilesAsync_WithAPinnedArchive_DownloadsFromThatExactArchive()
    {
        var handler = new RecordingHttpMessageHandler();
        var gameId = await SeedBaseGameAsync(handler);
        var pinnedArchiveId = Guid.NewGuid();

        handler.MapBytes(PinnedRoute(gameId, pinnedArchiveId), FakeApi.CreateZip((BaseEntry, "data")));

        var client = FakeApi.CreateGameClient(handler);

        await client.RestoreFilesAsync(_installDirectory, gameId, new[] { BaseEntry }, pinnedArchiveId);

        Assert.Contains(PinnedRoute(gameId, pinnedArchiveId), handler.Requests);
        Assert.True(File.Exists(Path.Combine(_installDirectory, BaseEntry)));
    }

    [Fact]
    public async Task RestoreFilesAsync_WithAPinnedArchive_NeverTouchesTheLatestArchiveEndpoint()
    {
        // The core of the finding: the un-gated "give me whatever is current" endpoint must not be
        // requested at all for an installation that knows exactly which archive it is running.
        var handler = new RecordingHttpMessageHandler();
        var gameId = await SeedBaseGameAsync(handler);
        var pinnedArchiveId = Guid.NewGuid();

        handler.MapBytes(PinnedRoute(gameId, pinnedArchiveId), FakeApi.CreateZip((BaseEntry, "data")));

        var client = FakeApi.CreateGameClient(handler);

        await client.RestoreFilesAsync(_installDirectory, gameId, new[] { BaseEntry }, pinnedArchiveId);

        Assert.DoesNotContain(LatestRoute(gameId), handler.Requests);
    }

    [Fact]
    public async Task RestoreFilesAsync_FromAnAddonFileListDiff_AlsoUsesThePinnedArchive()
    {
        // The overload InstallService.Modify() actually calls after an add-on install/uninstall.
        var handler = new RecordingHttpMessageHandler();
        var gameId = await SeedBaseGameAsync(handler);
        var pinnedArchiveId = Guid.NewGuid();

        handler.MapBytes(PinnedRoute(gameId, pinnedArchiveId), FakeApi.CreateZip((BaseEntry, "data")));

        var removed = new GameInstallationFileList(_installDirectory, gameId);
        removed.BaseGame.Files.Add(new GameInstallationFileListEntry.FileEntry
        {
            EntryPath = BaseEntry,
            LocalPath = Path.Combine(_installDirectory, BaseEntry),
        });

        var client = FakeApi.CreateGameClient(handler);

        await client.RestoreFilesAsync(_installDirectory, gameId, removed, GameInstallationFileList.Empty, pinnedArchiveId);

        Assert.Contains(PinnedRoute(gameId, pinnedArchiveId), handler.Requests);
        Assert.DoesNotContain(LatestRoute(gameId), handler.Requests);
    }

    [Fact]
    public async Task RestoreFilesAsync_WithoutAnArchiveId_KeepsUsingTheEffectiveDefaultEndpoint()
    {
        // Back-compat: callers that genuinely do not know which archive is installed (and the
        // pre-existing 3-argument overload) must keep working exactly as before.
        var handler = new RecordingHttpMessageHandler();
        var gameId = await SeedBaseGameAsync(handler);

        handler.MapBytes(LatestRoute(gameId), FakeApi.CreateZip((BaseEntry, "data")));

        var client = FakeApi.CreateGameClient(handler);

        await client.RestoreFilesAsync(_installDirectory, gameId, new[] { BaseEntry });

        Assert.Contains(LatestRoute(gameId), handler.Requests);
        Assert.True(File.Exists(Path.Combine(_installDirectory, BaseEntry)));
    }

    [Fact]
    public async Task RestoreFilesAsync_AddonOwnedConflicts_DoNotBorrowTheBaseGamesPinnedArchive()
    {
        // A conflict owned by an add-on overlaying the same directory has its own (unknown here)
        // archive identity — it must keep the previous effective-default behavior against its own
        // game id, and must certainly never be requested out of the *base game's* pinned archive.
        var handler = new RecordingHttpMessageHandler();
        var addonId = Guid.NewGuid();
        var gameId = await SeedBaseGameAsync(handler, addonId);
        var pinnedArchiveId = Guid.NewGuid();

        handler.MapBytes(PinnedRoute(gameId, pinnedArchiveId), FakeApi.CreateZip((BaseEntry, "data")));
        handler.MapBytes(LatestRoute(addonId), FakeApi.CreateZip((AddonEntry, "addon")));

        var client = FakeApi.CreateGameClient(handler);

        await client.RestoreFilesAsync(_installDirectory, gameId, new[] { BaseEntry, AddonEntry }, pinnedArchiveId);

        Assert.Contains(PinnedRoute(gameId, pinnedArchiveId), handler.Requests);
        Assert.Contains(LatestRoute(addonId), handler.Requests);

        // Neither the base game's latest archive nor the add-on pinned to the base's archive.
        Assert.DoesNotContain(LatestRoute(gameId), handler.Requests);
        Assert.DoesNotContain(PinnedRoute(addonId, pinnedArchiveId), handler.Requests);

        Assert.True(File.Exists(Path.Combine(_installDirectory, BaseEntry)));
        Assert.True(File.Exists(Path.Combine(_installDirectory, AddonEntry)));
    }

    [Fact]
    public async Task RestoreFilesAsync_WithNoEntries_MakesNoRequestsAtAll()
    {
        var handler = new RecordingHttpMessageHandler();
        var gameId = await SeedBaseGameAsync(handler);

        var client = FakeApi.CreateGameClient(handler);

        await client.RestoreFilesAsync(_installDirectory, gameId, Array.Empty<string>(), Guid.NewGuid());

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DownloadFilesAsync_WithAPinnedArchive_UsesTheGameScopedPinnedEndpoint()
    {
        // The lower-level primitive RestoreFilesAsync funnels into — asserted directly so the
        // archive plumbing is covered independently of file validation.
        var handler = new RecordingHttpMessageHandler();
        var gameId = Guid.NewGuid();
        var pinnedArchiveId = Guid.NewGuid();

        handler.MapBytes(PinnedRoute(gameId, pinnedArchiveId), FakeApi.CreateZip((BaseEntry, "data")));

        var client = FakeApi.CreateGameClient(handler);

        await client.DownloadFilesAsync(_installDirectory, gameId, new[] { BaseEntry }, pinnedArchiveId);

        Assert.Contains(PinnedRoute(gameId, pinnedArchiveId), handler.Requests);
        Assert.DoesNotContain(LatestRoute(gameId), handler.Requests);
        Assert.True(File.Exists(Path.Combine(_installDirectory, BaseEntry)));
    }

    [Fact]
    public async Task DownloadFilesAsync_GroupsPerGameAndArchive()
    {
        var handler = new RecordingHttpMessageHandler();
        var gameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var pinnedArchiveId = Guid.NewGuid();

        handler.MapBytes(PinnedRoute(gameId, pinnedArchiveId), FakeApi.CreateZip((BaseEntry, "data")));
        handler.MapBytes(LatestRoute(addonId), FakeApi.CreateZip((AddonEntry, "addon")));

        var client = FakeApi.CreateGameClient(handler);

        await client.DownloadFilesAsync(_installDirectory, new[]
        {
            (GameId: gameId, ArchiveId: (Guid?)pinnedArchiveId, FilePath: BaseEntry),
            (GameId: addonId, ArchiveId: (Guid?)null, FilePath: AddonEntry),
        });

        Assert.Contains(PinnedRoute(gameId, pinnedArchiveId), handler.Requests);
        Assert.Contains(LatestRoute(addonId), handler.Requests);
        Assert.Equal(2, handler.Requests.Count);
    }
}
