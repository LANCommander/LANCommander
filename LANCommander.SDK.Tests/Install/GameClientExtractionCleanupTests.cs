using System.Net;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using LANCommander.SDK.Tests.Helpers;
using SdkGame = LANCommander.SDK.Models.Game;

namespace LANCommander.SDK.Tests.Install;

/// <summary>
/// Regression coverage for the CRITICAL "a canceled or failed download deletes the whole existing
/// installation" finding.
///
/// <c>GameClient.DownloadAndExtractAsync</c> used to answer every cancel/HTTP/extraction failure
/// with <c>Directory.Delete(destination, true)</c>. That is only ever correct for a brand-new
/// install into a directory it created itself. Three real flows hand it a *populated* directory
/// instead — an in-place version change (<c>InstallService.Update</c>), a legacy/overlay
/// exact-directory update, and an add-on extracting into its base game's shared folder — so a
/// dropped connection, a canceled download, or one corrupt archive wiped out the user's working
/// installation (and, for an overlay, the base game plus every other add-on in that folder).
///
/// Cleanup ownership is now declared explicitly on the plan item
/// (<see cref="InstallDestinationOwnership"/>) rather than inferred, because a fresh destination
/// can legitimately be pre-created empty and so cannot be told apart from an existing installation
/// by an on-disk probe alone.
///
/// These tests drive the real <see cref="GameClient.ExecuteInstallPlanItemAsync"/> over a recording
/// HTTP stack that genuinely returns the failure, and assert on the bytes left on disk.
/// </summary>
public class GameClientExtractionCleanupTests : IDisposable
{
    private readonly string _root;

    public GameClientExtractionCleanupTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"lc-extract-cleanup-{Guid.NewGuid()}");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private const string SentinelName = "savegame.sav";
    private const string SentinelContent = "the user's existing installation";
    private const string NestedSentinelName = "saves/campaign.sav";

    private static string GameRoute(Guid gameId) => $"/api/Games/{gameId}";

    private static string PinnedDownloadRoute(Guid gameId, Guid archiveId) => $"/api/Games/{gameId}/Download?archiveId={archiveId}";

    private string Destination => Path.Combine(_root, "Half-Life");

    /// <summary>Creates the destination as an existing, populated installation directory.</summary>
    private void SeedExistingInstallation()
    {
        Directory.CreateDirectory(Path.Combine(Destination, "saves"));

        File.WriteAllText(Path.Combine(Destination, SentinelName), SentinelContent);
        File.WriteAllText(Path.Combine(Destination, NestedSentinelName), SentinelContent);
    }

    private (RecordingHttpMessageHandler Handler, Guid GameId, Guid ArchiveId) SeedGame()
    {
        var handler = new RecordingHttpMessageHandler();
        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        handler.MapJson(GameRoute(gameId), new SdkGame
        {
            Id = gameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            BaseGameId = Guid.Empty,
        });

        return (handler, gameId, archiveId);
    }

    private static InstallPlanItem MakeDownloadOnlyPlanItem(
        Guid gameId,
        Guid archiveId,
        string destination,
        InstallDestinationOwnership ownership) =>
        new()
        {
            EntityId = gameId,
            Title = "Half-Life",
            Type = InstallPlanItemType.Game,
            InstallDirectory = destination,
            ArchiveId = archiveId,
            ArchiveVersion = "1.0.0",
            DestinationOwnership = ownership,
            Tasks =
            [
                new InstallTaskDefinition
                {
                    Type = InstallTaskType.DownloadAndExtract,
                    Title = "Download Half-Life",
                    Order = 0,
                    TargetId = gameId,
                    TargetName = "Half-Life",
                    IsCritical = true,
                    ReportsProgress = true,
                },
            ],
        };

    private void AssertExistingInstallationIsIntact()
    {
        Assert.True(Directory.Exists(Destination));
        Assert.Equal(SentinelContent, File.ReadAllText(Path.Combine(Destination, SentinelName)));
        Assert.Equal(SentinelContent, File.ReadAllText(Path.Combine(Destination, NestedSentinelName)));
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_HttpFailure_LeavesAnExistingInstallationCompletelyIntact()
    {
        var (handler, gameId, archiveId) = SeedGame();
        SeedExistingInstallation();

        handler.Map(PinnedDownloadRoute(gameId, archiveId),
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(string.Empty) });

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.ExistingInstallation);

        await Assert.ThrowsAsync<InstallException>(() => client.ExecuteInstallPlanItemAsync(planItem));

        AssertExistingInstallationIsIntact();
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_CorruptArchive_LeavesAnExistingInstallationCompletelyIntact()
    {
        var (handler, gameId, archiveId) = SeedGame();
        SeedExistingInstallation();

        // A 200 whose body is not a readable archive at all — the "is it corrupted?" branch.
        handler.MapBytes(PinnedDownloadRoute(gameId, archiveId), "this is not an archive"u8.ToArray());

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.ExistingInstallation);

        await Assert.ThrowsAsync<InstallException>(() => client.ExecuteInstallPlanItemAsync(planItem));

        AssertExistingInstallationIsIntact();
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_Cancellation_LeavesAnExistingInstallationCompletelyIntact()
    {
        var (handler, gameId, archiveId) = SeedGame();
        SeedExistingInstallation();

        using var cts = new CancellationTokenSource();

        // Cancel exactly when the archive download is requested: the extraction that follows
        // observes the token, which is the flow a user hitting "cancel" mid-download produces.
        handler.Map(PinnedDownloadRoute(gameId, archiveId), _ =>
        {
            cts.Cancel();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FakeApi.CreateZip(("game.exe", "data"))),
            };
        });

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.ExistingInstallation);

        await Assert.ThrowsAsync<InstallCanceledException>(() => client.ExecuteInstallPlanItemAsync(planItem, cts.Token));

        AssertExistingInstallationIsIntact();
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_Cancellation_IsClassifiedAsCanceledRatherThanAFailedInstall()
    {
        // The classification itself matters independently of cleanup: a cancellation surfaced as a
        // generic extraction failure gets retried by RetryHelper (re-downloading a canceled
        // install) and reported to the user as a possibly-corrupt archive.
        var (handler, gameId, archiveId) = SeedGame();

        using var cts = new CancellationTokenSource();
        var downloadRequests = 0;

        handler.Map(PinnedDownloadRoute(gameId, archiveId), _ =>
        {
            downloadRequests++;
            cts.Cancel();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(FakeApi.CreateZip(("game.exe", "data"))),
            };
        });

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 5);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.Fresh);

        await Assert.ThrowsAsync<InstallCanceledException>(() => client.ExecuteInstallPlanItemAsync(planItem, cts.Token));

        // A cancellation is terminal: it must not be retried as if it were a transport failure.
        Assert.Equal(1, downloadRequests);
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_FailedFreshInstall_CleansUpTheDestinationItOwns()
    {
        // The behavior that must be preserved: a fresh install into a directory that did not exist
        // still tidies up after itself instead of leaving a half-extracted folder behind.
        var (handler, gameId, archiveId) = SeedGame();

        handler.MapBytes(PinnedDownloadRoute(gameId, archiveId), "this is not an archive"u8.ToArray());

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.Fresh);

        Assert.False(Directory.Exists(Destination));

        await Assert.ThrowsAsync<InstallException>(() => client.ExecuteInstallPlanItemAsync(planItem));

        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_FailedFreshInstall_CleansUpADestinationThatWasPreCreatedEmpty()
    {
        // A fresh destination may legitimately already exist as an empty directory (pre-created by
        // the caller, or left behind by a previous attempt). It is still owned by this install, so
        // "the directory exists" alone must not be what decides cleanup.
        var (handler, gameId, archiveId) = SeedGame();

        Directory.CreateDirectory(Destination);

        handler.MapBytes(PinnedDownloadRoute(gameId, archiveId), "this is not an archive"u8.ToArray());

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.Fresh);

        await Assert.ThrowsAsync<InstallException>(() => client.ExecuteInstallPlanItemAsync(planItem));

        Assert.False(Directory.Exists(Destination));
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_FreshItemPointedAtAPopulatedDirectory_StillRefusesToDeleteIt()
    {
        // Defense in depth for the other direction: if a caller ever mislabels a destination as
        // fresh while real files are sitting in it, the observed state wins and nothing is deleted.
        var (handler, gameId, archiveId) = SeedGame();
        SeedExistingInstallation();

        handler.MapBytes(PinnedDownloadRoute(gameId, archiveId), "this is not an archive"u8.ToArray());

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);
        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.Fresh);

        await Assert.ThrowsAsync<InstallException>(() => client.ExecuteInstallPlanItemAsync(planItem));

        AssertExistingInstallationIsIntact();
    }

    [Fact]
    public async Task ExecuteInstallPlanItem_DefaultsToNeverDeletingTheDestination()
    {
        // An InstallPlanItem that never declares ownership must get the safe answer: leaving
        // partial files behind is recoverable, deleting an installation is not.
        var (handler, gameId, archiveId) = SeedGame();
        SeedExistingInstallation();

        handler.MapBytes(PinnedDownloadRoute(gameId, archiveId), "this is not an archive"u8.ToArray());

        var client = FakeApi.CreateGameClient(handler, maxInstallAttempts: 1);

        var planItem = MakeDownloadOnlyPlanItem(gameId, archiveId, Destination, InstallDestinationOwnership.ExistingInstallation);
        planItem.DestinationOwnership = new InstallPlanItem().DestinationOwnership;

        Assert.Equal(InstallDestinationOwnership.ExistingInstallation, planItem.DestinationOwnership);

        await Assert.ThrowsAsync<InstallException>(() => client.ExecuteInstallPlanItemAsync(planItem));

        AssertExistingInstallationIsIntact();
    }

    [Fact]
    public async Task GenerateInstallPlan_MarksAddonItemsAsSharingTheBaseGamesDirectory()
    {
        // An add-on overlays the base game's folder — it never owns it, so a failed add-on download
        // must not be able to delete the base game (and every sibling add-on) with it.
        var handler = new RecordingHttpMessageHandler();
        var gameId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();
        var addonArchiveId = Guid.NewGuid();

        handler.MapJson(GameRoute(gameId), new SdkGame { Id = gameId, Title = "Half-Life", Type = GameType.MainGame });
        handler.MapJson(GameRoute(addonId), new SdkGame { Id = addonId, Title = "Opposing Force", Type = GameType.Expansion, BaseGameId = gameId });
        handler.MapJson($"/api/Games/{gameId}/Archives/Resolve?archiveId={archiveId}", new Archive { Id = archiveId, Version = "1.0.0" });
        handler.MapJson($"/api/Games/{addonId}/Archives/Resolve", new Archive { Id = addonArchiveId, Version = "2.0.0" });
        handler.MapJson($"/api/Games/{gameId}/Tools", Array.Empty<object>());

        var client = FakeApi.CreateGameClient(handler);

        var plan = await client.GenerateInstallPlanAsync(
            gameId,
            Destination,
            addonIds: [addonId],
            archiveId: archiveId,
            useExactInstallDirectory: true,
            destinationOwnership: InstallDestinationOwnership.Fresh);

        var gameItem = Assert.Single(plan.Items, i => i.Type == InstallPlanItemType.Game);
        var addonItem = Assert.Single(plan.Items, i => i.Type == InstallPlanItemType.Addon);

        Assert.Equal(InstallDestinationOwnership.Fresh, gameItem.DestinationOwnership);
        Assert.Equal(InstallDestinationOwnership.ExistingInstallation, addonItem.DestinationOwnership);
        Assert.Equal(gameItem.InstallDirectory, addonItem.InstallDirectory);
    }

    [Fact]
    public async Task GenerateInstallPlan_CarriesAnExistingInstallationDestinationOntoTheGameItem()
    {
        var handler = new RecordingHttpMessageHandler();
        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        handler.MapJson(GameRoute(gameId), new SdkGame { Id = gameId, Title = "Half-Life", Type = GameType.MainGame });
        handler.MapJson($"/api/Games/{gameId}/Archives/Resolve?archiveId={archiveId}", new Archive { Id = archiveId, Version = "1.0.0" });
        handler.MapJson($"/api/Games/{gameId}/Tools", Array.Empty<object>());

        var client = FakeApi.CreateGameClient(handler);

        var plan = await client.GenerateInstallPlanAsync(
            gameId,
            Destination,
            archiveId: archiveId,
            useExactInstallDirectory: true,
            destinationOwnership: InstallDestinationOwnership.ExistingInstallation);

        var gameItem = Assert.Single(plan.Items, i => i.Type == InstallPlanItemType.Game);

        Assert.Equal(InstallDestinationOwnership.ExistingInstallation, gameItem.DestinationOwnership);
    }

    [Fact]
    public async Task ToolInstall_WhenTheArchiveIsCorrupt_LeavesTheGamesInstallDirectoryIntact()
    {
        // A tool always extracts into a game's existing install directory, so the same recursive
        // cleanup here would delete the entire game — saves, add-ons and all — because one optional
        // tool failed to download.
        var handler = new RecordingHttpMessageHandler();
        var toolId = Guid.NewGuid();

        SeedExistingInstallation();

        handler.MapJson($"/api/Tools/{toolId}", new Tool { Id = toolId, Name = "Mod Loader" });

        // The availability HEAD succeeds; the actual download body is not a readable archive, so
        // extraction fails after the destination has already been opened for writing.
        handler.Map($"/api/Tools/{toolId}/Download", request => request.Method == HttpMethod.Head
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("this is not an archive"u8.ToArray()) });

        var client = FakeApi.CreateToolClient(handler);

        var planItem = new InstallPlanItem
        {
            EntityId = toolId,
            Title = "Mod Loader",
            Type = InstallPlanItemType.Tool,
            InstallDirectory = Destination,
            Tasks =
            [
                new InstallTaskDefinition
                {
                    Type = InstallTaskType.DownloadAndExtract,
                    Title = "Download Mod Loader",
                    Order = 0,
                    TargetId = toolId,
                    TargetName = "Mod Loader",
                    IsCritical = true,
                    ReportsProgress = true,
                },
            ],
        };

        await Assert.ThrowsAnyAsync<Exception>(() => client.ExecuteInstallPlanItemAsync(planItem));

        AssertExistingInstallationIsIntact();
    }

    [Fact]
    public void ToolPlanItems_NeverClaimOwnershipOfTheirDestination()
    {
        // A tool's destination is always supplied by a caller (normally the game's own install
        // directory), so a tool plan item must never end up declaring it owns it.
        var toolItem = new InstallPlanItem
        {
            EntityId = Guid.NewGuid(),
            Type = InstallPlanItemType.Tool,
            InstallDirectory = Destination,
        };

        Assert.Equal(InstallDestinationOwnership.ExistingInstallation, toolItem.DestinationOwnership);
    }
}
