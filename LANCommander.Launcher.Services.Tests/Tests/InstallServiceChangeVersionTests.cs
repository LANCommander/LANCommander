using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services.Tests.Helpers;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// Regression coverage for the HIGH "Change Version side-by-side to an already-installed archive"
/// bug: <see cref="InstallService.ChangeVersionAsync(GameInstallation, Guid, bool)"/> used to fall
/// straight through to <see cref="InstallService.Add"/>, which — finding an installation already
/// pinned to the requested archive — routed into <see cref="InstallService.Modify"/> with no
/// addon/tool selection of its own to supply, which (before the null-vs-empty fix) uninstalled
/// every addon/tool from that unrelated installation. ChangeVersionAsync must now short-circuit
/// before ever reaching <see cref="InstallService.Add"/>: a no-op when the target archive is
/// already exactly what's installed (in-place or side-by-side), and a clear
/// <see cref="InvalidOperationException"/> when a *different* installation already has it.
///
/// All three scenarios covered here resolve (no-op or throw) using only DB-backed lookups
/// (<c>GameService.GetAsync</c>, <c>GameInstallationService.FindByArchiveAsync</c>) — before
/// ChangeVersionAsync/Add would ever call any network-bound GameClient method — so these run
/// safely against the null-network-dependency GameClient the other InstallService tests use.
/// </summary>
public class InstallServiceChangeVersionTests
{
    private static InstallService CreateInstallService(out GameInstallationService installationService, out Data.DatabaseContext context)
    {
        context = InMemoryDatabaseFactory.Create();
        installationService = ServiceTestFactory.CreateGameInstallationService(context);
        var toolService = ServiceTestFactory.CreateToolService(context);
        var gameService = ServiceTestFactory.CreateGameService(context, toolService, installationService);

        return ServiceTestFactory.CreateInstallService(gameService, toolService, installationService);
    }

    private static async Task<Game> SeedGameAsync(Data.DatabaseContext context)
    {
        var game = new Game { Id = Guid.NewGuid(), Title = "Half-Life" };
        context.Games!.Add(game);
        await context.SaveChangesAsync();
        return game;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangeVersionAsync_TargetArchiveAlreadyCurrent_IsANoOp_InPlaceOrSideBySide(bool inPlace)
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var game = await SeedGameAsync(context);
        var archiveId = Guid.NewGuid();

        var installation = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = @"C:\Games\HalfLife",
            ArchiveId = archiveId,
            Version = "1.0.0",
            InstalledOn = DateTime.UtcNow,
        };
        await installationService.AddInstallationAsync(installation);

        // Must return without throwing and without touching the queue or the network-bound
        // GameClient at all — asking to "change" to the version already installed is a no-op.
        await installService.ChangeVersionAsync(installation, archiveId, inPlace);

        installService.Queue.ShouldBeEmpty();

        var reloaded = await installationService.GetAsync(installation.Id);
        reloaded!.ArchiveId.ShouldBe(archiveId);
        reloaded.Version.ShouldBe("1.0.0");
        reloaded.InstallDirectory.ShouldBe(@"C:\Games\HalfLife");
    }

    [Fact]
    public async Task ChangeVersionAsync_SideBySideTargetAlreadyInstalledElsewhere_ThrowsInsteadOfSilentlyModifyingIt()
    {
        var installService = CreateInstallService(out var installationService, out var context);
        var game = await SeedGameAsync(context);
        var archiveA = Guid.NewGuid();
        var archiveB = Guid.NewGuid();

        var installationA = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = @"C:\Games\HalfLife",
            ArchiveId = archiveA,
            Version = "1.0.0",
            InstalledOn = DateTime.UtcNow,
        };
        await installationService.AddInstallationAsync(installationA);

        var installationB = new GameInstallation
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            InstallDirectory = @"C:\Games\HalfLife (2.0.0)",
            ArchiveId = archiveB,
            Version = "2.0.0",
            InstalledOn = DateTime.UtcNow,
        };
        await installationService.AddInstallationAsync(installationB, select: false);

        // From installationA, asking to "change version" (side-by-side) to archiveB — which is
        // already installed as installationB — must fail loudly rather than silently routing
        // into Modify() for installationB with no addon/tool selection of its own.
        var ex = await Should.ThrowAsync<InvalidOperationException>(
            () => installService.ChangeVersionAsync(installationA, archiveB, inPlace: false));

        ex.Message.ShouldContain(installationB.InstallDirectory);

        installService.Queue.ShouldBeEmpty();

        // Neither installation's state may have been touched.
        var reloadedA = await installationService.GetAsync(installationA.Id);
        reloadedA!.ArchiveId.ShouldBe(archiveA);
        reloadedA.InstallDirectory.ShouldBe(@"C:\Games\HalfLife");

        var reloadedB = await installationService.GetAsync(installationB.Id);
        reloadedB!.ArchiveId.ShouldBe(archiveB);
        reloadedB.InstallDirectory.ShouldBe(@"C:\Games\HalfLife (2.0.0)");
    }

    [Fact]
    public async Task ChangeVersionAsync_NullInstallation_ThrowsArgumentNullException()
    {
        var installService = CreateInstallService(out _, out _);

        await Should.ThrowAsync<ArgumentNullException>(
            () => installService.ChangeVersionAsync(installation: null!, Guid.NewGuid(), inPlace: false));
    }
}
