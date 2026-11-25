using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services.Tests.Helpers;
using LANCommander.SDK.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Services.Tests.Tests;

/// <summary>
/// An installed game whose offered version id was never stored falls back to comparing version labels, so an
/// install on the latest version can show an update when its labels differ. Library sync has to re-import it,
/// and an update's save must not put the stale values back over that import.
/// </summary>
public class GameVersionCatchUpTests
{
    private readonly DbContextOptions _options = new DbContextOptionsBuilder()
        .UseInMemoryDatabase($"GameVersionCatchUpTests-{Guid.NewGuid()}")
        .Options;

    private DatabaseContext CreateContext() => new(NullLoggerFactory.Instance, _options);

    private static GameService CreateGameService(DatabaseContext context)
        => new(context, NullLogger<GameService>.Instance, null!, null!, null!, null!, null!, null!, null!, null!, null!);

    private static readonly Guid VersionId = Guid.NewGuid();

    private async Task<Data.Models.Game> SeedStaleInstallAsync()
    {
        // Installed on the offered version, recorded by its id and label; the latest version was imported
        // before its id was stored, carrying the archive's label instead
        var game = GameFactory.Make("Recoil", installed: true);
        game.InstalledVersion = "1.0.6.8-reRECOIL";
        game.InstalledVersionId = VersionId;
        game.LatestVersion = "1.0.6.8";
        game.LatestVersionId = null;
        game.ImportedOn = DateTime.UtcNow;

        await using var context = CreateContext();
        context.Games!.Add(game);
        await context.SaveChangesAsync();

        return game;
    }

    [Fact]
    public async Task Installed_game_without_a_latest_version_id_is_reimported_though_unchanged()
    {
        var game = await SeedStaleInstallAsync();

        await using var context = CreateContext();
        var gameService = CreateGameService(context);

        var importedOn = await gameService.GetImportedOnMapAsync([game.Id]);
        var missing = await gameService.GetIdsMissingLatestVersionIdAsync([game.Id]);

        var reference = new EntityReference { Id = game.Id, UpdatedOn = game.ImportedOn.AddDays(-1) };

        missing.ShouldContain(game.Id);
        ImportService.NeedsImport(reference, importedOn, missing).ShouldBeTrue();
    }

    [Fact]
    public async Task Game_without_an_installed_version_id_is_not_reimported_on_every_sync()
    {
        // A server without versions never sends an id, so re-importing would find nothing new each sync
        var game = GameFactory.Make("Old Server Game", installed: true);
        game.InstalledVersion = "1.0";
        game.LatestVersion = "1.0";
        game.ImportedOn = DateTime.UtcNow;

        await using (var seed = CreateContext())
        {
            seed.Games!.Add(game);
            await seed.SaveChangesAsync();
        }

        await using var context = CreateContext();
        var gameService = CreateGameService(context);

        var importedOn = await gameService.GetImportedOnMapAsync([game.Id]);
        var missing = await gameService.GetIdsMissingLatestVersionIdAsync([game.Id]);

        var reference = new EntityReference { Id = game.Id, UpdatedOn = game.ImportedOn.AddDays(-1) };

        missing.ShouldBeEmpty();
        ImportService.NeedsImport(reference, importedOn, missing).ShouldBeFalse();
    }

    [Fact]
    public void New_and_changed_games_are_imported()
    {
        var importedOn = DateTime.UtcNow;
        var known = Guid.NewGuid();
        var map = new Dictionary<Guid, DateTime> { [known] = importedOn };
        var none = new HashSet<Guid>();

        ImportService.NeedsImport(new EntityReference { Id = Guid.NewGuid(), UpdatedOn = importedOn }, map, none).ShouldBeTrue();
        ImportService.NeedsImport(new EntityReference { Id = known, UpdatedOn = importedOn.AddMinutes(1) }, map, none).ShouldBeTrue();
        ImportService.NeedsImport(new EntityReference { Id = known, UpdatedOn = importedOn }, map, none).ShouldBeFalse();
    }

    [Fact]
    public async Task Saving_after_a_reload_keeps_what_another_scope_imported()
    {
        var game = await SeedStaleInstallAsync();

        // The update's scope already tracks the game
        await using var updateContext = CreateContext();
        var updateGames = CreateGameService(updateContext);
        var localGame = await updateGames.GetAsync(game.Id);

        // The import runs in its own scope and stores the offered version
        await using (var importContext = CreateContext())
        {
            var imported = await importContext.Games!.SingleAsync(g => g.Id == game.Id);
            imported.LatestVersion = "1.0.6.8-reRECOIL";
            imported.LatestVersionId = VersionId;
            await importContext.SaveChangesAsync();
        }

        localGame = await updateGames.ReloadAsync(localGame);
        localGame.ShouldNotBeNull();
        localGame.LatestVersionId.ShouldBe(VersionId);

        await updateGames.UpdateAsync(localGame);

        await using var verifyContext = CreateContext();
        var stored = await verifyContext.Games!.SingleAsync(g => g.Id == game.Id);

        stored.LatestVersionId.ShouldBe(VersionId);
        stored.LatestVersion.ShouldBe("1.0.6.8-reRECOIL");
        stored.IsUpdateAvailable().ShouldBeFalse();
    }
}
