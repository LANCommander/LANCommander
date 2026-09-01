using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Enums;
using Shouldly;
using Xunit;
using SdkGame = LANCommander.SDK.Models.Game;
using SdkArchive = LANCommander.SDK.Models.Archive;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Regression coverage for the HIGH "update of an installed overlay/legacy-only entry fails
/// outright" finding.
///
/// Overlay add-ons (Expansion/Mod/StandaloneMod with a base game) install into their base game's
/// directory and are deliberately never given their own <see cref="GameInstallation"/> row — the
/// AddGameInstallations migration excludes them, because a second row at the base game's directory
/// would break the install-directory uniqueness invariant. Pre-migration/legacy installs may not
/// have a row yet either. Both are still reported as installed (from the legacy Game fields), so
/// the action bar's PrimaryAction routes their "update available" state into the update flow —
/// which used to throw "No installation found to update" for exactly these entries.
///
/// The restored flow queues an <see cref="InstallService.Add"/> against the entry's own existing
/// directory, passed as the <em>exact</em> destination. These tests drive the real service over a
/// recording HTTP stack and assert the queued destination, so both halves — "it queues at all" and
/// "it queues against the right folder" — are covered.
/// </summary>
public class InstallServiceLegacyUpdateTests
{
    private const string SharedDirectory = @"C:\Games\Half-Life";

    private sealed record Fixture(
        InstallService InstallService,
        GameInstallationService InstallationService,
        Data.DatabaseContext Context,
        RecordingHttpMessageHandler Handler);

    private static Fixture CreateFixture()
    {
        var handler = new RecordingHttpMessageHandler();
        var context = InMemoryDatabaseFactory.Create();
        var gameClient = FakeApiFactory.CreateGameClient(handler);
        var installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService, gameClient);
        var installService = ServiceTestFactory.CreateInstallService(gameService, toolService, installationService, gameClient);

        // Park a permanently "active" item in the queue so Add() never auto-starts processing:
        // these tests are about what gets *queued*, not about executing a real download.
        installService.Queue.Add(new InstallQueueGame(new SdkGame { Id = Guid.NewGuid(), Title = "Busy" })
        {
            Status = InstallStatus.Downloading,
        });

        return new Fixture(installService, installationService, context, handler);
    }

    private static void MapGame(RecordingHttpMessageHandler handler, SdkGame game, SdkArchive archive)
    {
        handler.MapJson(FakeApiFactory.GameRoute(game.Id), game);

        // Add() resolves the effective default once, then GenerateInstallPlanAsync re-resolves the
        // now-pinned id — both must answer with the same archive.
        handler.MapJson(FakeApiFactory.ResolveArchiveRoute(game.Id), archive);
        handler.MapJson(FakeApiFactory.ResolveArchiveRoute(game.Id, archive.Id), archive);
    }

    private static InstallQueueGame QueuedItemFor(InstallService installService, Guid entityId) =>
        installService.Queue.OfType<InstallQueueGame>().Single(i => i.EntityId == entityId);

    [Fact]
    public async Task Add_ForAnInstalledOverlayWithNoInstallationRow_QueuesAgainstItsExactExistingDirectory()
    {
        var fixture = CreateFixture();
        var baseGameId = Guid.NewGuid();
        var overlayId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        // Base game legacy-installed at the shared directory, so Add() does not try to install it.
        fixture.Context.Games!.Add(new Game
        {
            Id = baseGameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Installed = true,
            InstallDirectory = SharedDirectory,
        });

        var overlay = new Game
        {
            Id = overlayId,
            Title = "Opposing Force",
            Type = GameType.Expansion,
            BaseGameId = baseGameId,
            Installed = true,
            InstallDirectory = SharedDirectory,
            InstalledVersion = "1.0.0",
        };

        fixture.Context.Games!.Add(overlay);
        await fixture.Context.SaveChangesAsync();

        MapGame(fixture.Handler,
            new SdkGame { Id = overlayId, Title = "Opposing Force", Type = GameType.Expansion, BaseGameId = baseGameId },
            new SdkArchive { Id = archiveId, Version = "2.0.0" });

        await fixture.InstallService.Add(overlay, SharedDirectory, useExactInstallDirectory: true);

        var queued = QueuedItemFor(fixture.InstallService, overlayId);

        queued.InstallDirectory.ShouldBe(SharedDirectory);
        queued.ArchiveId.ShouldBe(archiveId);
    }

    [Fact]
    public async Task Add_ForAnInstalledOverlay_NeverNestsInsideItsOwnDirectory()
    {
        // The specific destructive shape the exact-directory path exists to prevent: re-deriving
        // "<existing>/<Title>" and installing a nested copy underneath the real installation.
        var fixture = CreateFixture();
        var baseGameId = Guid.NewGuid();
        var overlayId = Guid.NewGuid();

        fixture.Context.Games!.Add(new Game
        {
            Id = baseGameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Installed = true,
            InstallDirectory = SharedDirectory,
        });

        var overlay = new Game
        {
            Id = overlayId,
            Title = "Opposing Force",
            Type = GameType.Expansion,
            BaseGameId = baseGameId,
            Installed = true,
            InstallDirectory = SharedDirectory,
        };

        fixture.Context.Games!.Add(overlay);
        await fixture.Context.SaveChangesAsync();

        MapGame(fixture.Handler,
            new SdkGame { Id = overlayId, Title = "Opposing Force", Type = GameType.Expansion, BaseGameId = baseGameId },
            new SdkArchive { Id = Guid.NewGuid(), Version = "2.0.0" });

        await fixture.InstallService.Add(overlay, SharedDirectory, useExactInstallDirectory: true);

        var queued = QueuedItemFor(fixture.InstallService, overlayId);

        queued.InstallDirectory.ShouldNotBe(Path.Combine(SharedDirectory, "Opposing Force"));
        queued.InstallDirectory.ShouldNotStartWith(SharedDirectory + Path.DirectorySeparatorChar);
    }

    [Fact]
    public async Task Add_ForALegacyMainGameWithNoInstallationRow_KeepsItsExactExistingDirectory()
    {
        var fixture = CreateFixture();
        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        var legacyGame = new Game
        {
            Id = gameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Installed = true,
            InstallDirectory = SharedDirectory,
            InstalledVersion = "0.9.0",
        };

        fixture.Context.Games!.Add(legacyGame);
        await fixture.Context.SaveChangesAsync();

        MapGame(fixture.Handler,
            new SdkGame { Id = gameId, Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty },
            new SdkArchive { Id = archiveId, Version = "1.0.0" });

        await fixture.InstallService.Add(legacyGame, SharedDirectory, useExactInstallDirectory: true);

        var queued = QueuedItemFor(fixture.InstallService, gameId);

        queued.InstallDirectory.ShouldBe(SharedDirectory);
    }

    [Fact]
    public async Task Add_WithoutTheExactDirectoryFlag_TreatsTheDirectoryAsAParentAndDivertsElsewhere()
    {
        // Contrast case proving the flag is load-bearing rather than incidental: handing the same
        // legacy directory in as an ordinary parent hint re-suffixes it with the game's title, so
        // the request would install into a *different* folder than the one already installed.
        var fixture = CreateFixture();
        var gameId = Guid.NewGuid();

        var legacyGame = new Game
        {
            Id = gameId,
            Title = "Half-Life",
            Type = GameType.MainGame,
            Installed = true,
            InstallDirectory = SharedDirectory,
        };

        fixture.Context.Games!.Add(legacyGame);
        await fixture.Context.SaveChangesAsync();

        MapGame(fixture.Handler,
            new SdkGame { Id = gameId, Title = "Half-Life", Type = GameType.MainGame, BaseGameId = Guid.Empty },
            new SdkArchive { Id = Guid.NewGuid(), Version = "1.0.0" });

        await fixture.InstallService.Add(legacyGame, SharedDirectory);

        var queued = QueuedItemFor(fixture.InstallService, gameId);

        queued.InstallDirectory.ShouldNotBe(SharedDirectory);
        queued.InstallDirectory.ShouldStartWith(SharedDirectory);
    }

    [Fact]
    public async Task Add_ForAnInstalledOverlay_ResolvesTheServersEffectiveDefaultArchive()
    {
        // Matches the pre-installation-instances update behavior: no explicit archive is requested,
        // so the server's effective default is resolved exactly once and pinned onto the plan.
        var fixture = CreateFixture();
        var baseGameId = Guid.NewGuid();
        var overlayId = Guid.NewGuid();
        var effectiveDefaultArchiveId = Guid.NewGuid();

        fixture.Context.Games!.Add(new Game
        {
            Id = baseGameId,
            Title = "Half-Life",
            Installed = true,
            InstallDirectory = SharedDirectory,
        });

        var overlay = new Game
        {
            Id = overlayId,
            Title = "Opposing Force",
            Type = GameType.Mod,
            BaseGameId = baseGameId,
            Installed = true,
            InstallDirectory = SharedDirectory,
        };

        fixture.Context.Games!.Add(overlay);
        await fixture.Context.SaveChangesAsync();

        MapGame(fixture.Handler,
            new SdkGame { Id = overlayId, Title = "Opposing Force", Type = GameType.Mod, BaseGameId = baseGameId },
            new SdkArchive { Id = effectiveDefaultArchiveId, Version = "3.1.0", IsEffectiveDefault = true });

        await fixture.InstallService.Add(overlay, SharedDirectory, useExactInstallDirectory: true);

        var queued = QueuedItemFor(fixture.InstallService, overlayId);

        queued.ArchiveId.ShouldBe(effectiveDefaultArchiveId);
        queued.ArchiveVersion.ShouldBe("3.1.0");

        // Resolved through the archive-resolution endpoint rather than any client-side "latest"
        // derivation.
        fixture.Handler.Requests.ShouldContain(FakeApiFactory.ResolveArchiveRoute(overlayId));
    }

    // ── FinalizeGameInstallStateAsync: overlays never get their own installation row ─────────

    [Fact]
    public async Task FinalizeGameInstallState_ForAnOverlayWhoseBaseHasNoRow_DoesNotCreateAConflictingInstallation()
    {
        // The base game being legacy-only (no GameInstallation row of its own) means the shared
        // directory is not "in use" by any row, which used to let the overlay create a row for it.
        // That row would then collide with the base game's own directory the moment it got one.
        var fixture = CreateFixture();
        var baseGameId = Guid.NewGuid();
        var overlayId = Guid.NewGuid();

        var overlay = new Game
        {
            Id = overlayId,
            Title = "Opposing Force",
            Type = GameType.Expansion,
            BaseGameId = baseGameId,
        };

        fixture.Context.Games!.Add(new Game { Id = baseGameId, Title = "Half-Life", Installed = true, InstallDirectory = SharedDirectory });
        fixture.Context.Games!.Add(overlay);
        await fixture.Context.SaveChangesAsync();

        var queueItem = new InstallQueueGame(new SdkGame { Id = overlayId, Title = "Opposing Force" })
        {
            InstallDirectory = SharedDirectory,
            Version = "2.0.0",
            ArchiveId = Guid.NewGuid(),
        };

        await fixture.InstallService.FinalizeGameInstallStateAsync(queueItem, overlay);

        (await fixture.InstallationService.HasInstallationsAsync(overlayId)).ShouldBeFalse();

        var reloaded = await fixture.Context.Games!.FindAsync(overlayId);
        reloaded!.Installed.ShouldBeTrue();
        reloaded.InstallDirectory.ShouldBe(SharedDirectory);
        reloaded.InstalledVersion.ShouldBe("2.0.0");
    }

    [Fact]
    public async Task FinalizeGameInstallState_ForANonOverlayLegacyGame_StillHealsItIntoAnInstallationRow()
    {
        // Contrast: a plain main game with no row *should* gain one — the overlay carve-out must
        // not accidentally suppress that.
        var fixture = CreateFixture();
        var gameId = Guid.NewGuid();
        var archiveId = Guid.NewGuid();

        var localGame = new Game { Id = gameId, Title = "Half-Life", Type = GameType.MainGame };

        fixture.Context.Games!.Add(localGame);
        await fixture.Context.SaveChangesAsync();

        var queueItem = new InstallQueueGame(new SdkGame { Id = gameId, Title = "Half-Life" })
        {
            InstallDirectory = SharedDirectory,
            Version = "1.0.0",
            ArchiveId = archiveId,
        };

        await fixture.InstallService.FinalizeGameInstallStateAsync(queueItem, localGame);

        var installations = await fixture.InstallationService.GetInstallationsForGameAsync(gameId);
        var installation = installations.ShouldHaveSingleItem();

        installation.InstallDirectory.ShouldBe(SharedDirectory);
        installation.ArchiveId.ShouldBe(archiveId);
    }

    // ── IsOverlayInstall ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(GameType.Expansion)]
    [InlineData(GameType.Mod)]
    [InlineData(GameType.StandaloneMod)]
    public void IsOverlayInstall_ForAnOverlayTypeWithABaseGame_IsTrue(GameType type)
    {
        var game = new Game { Id = Guid.NewGuid(), Type = type, BaseGameId = Guid.NewGuid() };

        InstallService.IsOverlayInstall(game).ShouldBeTrue();
    }

    [Fact]
    public void IsOverlayInstall_WithoutABaseGame_IsFalse()
    {
        InstallService.IsOverlayInstall(new Game { Id = Guid.NewGuid(), Type = GameType.Expansion }).ShouldBeFalse();
        InstallService.IsOverlayInstall(new Game { Id = Guid.NewGuid(), Type = GameType.Expansion, BaseGameId = Guid.Empty }).ShouldBeFalse();
    }

    [Fact]
    public void IsOverlayInstall_ForAMainGameOrStandaloneExpansion_IsFalse()
    {
        InstallService.IsOverlayInstall(new Game { Id = Guid.NewGuid(), Type = GameType.MainGame, BaseGameId = Guid.NewGuid() }).ShouldBeFalse();
        InstallService.IsOverlayInstall(new Game { Id = Guid.NewGuid(), Type = GameType.StandaloneExpansion, BaseGameId = Guid.NewGuid() }).ShouldBeFalse();
    }

    [Fact]
    public void IsOverlayInstall_ForNull_IsFalse()
    {
        InstallService.IsOverlayInstall(null).ShouldBeFalse();
    }
}
