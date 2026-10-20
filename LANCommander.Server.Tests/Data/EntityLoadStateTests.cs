using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace LANCommander.Server.Tests.Data;

/// <summary>
/// Direct coverage of the load-state registry that lets the update path tell an unloaded
/// navigation apart from one the caller deliberately emptied.
/// </summary>
[Collection("Application")]
public class EntityLoadStateTests(ApplicationFixture fixture) : DalTest(fixture)
{
    IDbContextFactory<DatabaseContext> ContextFactory => GetService<IDbContextFactory<DatabaseContext>>();

    [Fact]
    public async Task AnEntityNobodyRecordedIsNeverReportedAsUnloaded()
    {
        // The fallback that keeps hand-built entities on their previous behavior.
        var game = new Game { Id = Guid.NewGuid(), Title = Unique("Game") };

        EntityLoadState.IsKnownUnloaded(game, nameof(Game.Archives)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(game, nameof(Game.Tags)).ShouldBeFalse();
    }

    [Fact]
    public async Task IncludedNavigationsAreRecordedAsLoadedAndOmittedOnesAreNot()
    {
        var game = await AddGameAsync();

        using var context = await ContextFactory.CreateDbContextAsync();

        var loaded = await context.Games
            .Include(g => g.Archives)
            .FirstAsync(g => g.Id == game.Id);

        EntityLoadState.Record(context);

        EntityLoadState.IsKnownUnloaded(loaded, nameof(Game.Archives)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(loaded, nameof(Game.Tags)).ShouldBeTrue();
        EntityLoadState.IsKnownUnloaded(loaded, nameof(Game.Scripts)).ShouldBeTrue();
    }

    [Fact]
    public async Task AQueryWithNoIncludesRecordsThatNothingWasLoaded()
    {
        var game = await AddGameAsync();

        using var context = await ContextFactory.CreateDbContextAsync();

        var loaded = await context.Games.FirstAsync(g => g.Id == game.Id);

        EntityLoadState.Record(context);

        EntityLoadState.IsKnownUnloaded(loaded, nameof(Game.Archives)).ShouldBeTrue();
        EntityLoadState.IsKnownUnloaded(loaded, nameof(Game.Tags)).ShouldBeTrue();
    }

    [Fact]
    public async Task NoTrackingResultsAreLeftUnrecordedSoTheyFallBack()
    {
        var game = await AddGameAsync();

        using var context = await ContextFactory.CreateDbContextAsync();

        var loaded = await context.Games
            .AsNoTracking()
            .FirstAsync(g => g.Id == game.Id);

        EntityLoadState.Record(context);

        // Nothing is tracked, so there is no state to record and the caller falls back to its
        // previous behavior rather than skipping every relationship.
        EntityLoadState.IsKnownUnloaded(loaded, nameof(Game.Archives)).ShouldBeFalse();
    }

    [Fact]
    public async Task ChildEntitiesPulledInByAnIncludeAreRecordedToo()
    {
        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);

        using var context = await ContextFactory.CreateDbContextAsync();

        await context.Games
            .Include(g => g.Archives)
            .FirstAsync(g => g.Id == game.Id);

        EntityLoadState.Record(context);

        var trackedArchive = context.ChangeTracker
            .Entries<Archive>()
            .Select(e => e.Entity)
            .First(a => a.Id == archive.Id);

        // Archive has no collection navigations of its own, so everything on it reads as unloaded
        // rather than as unknown. What matters is that the entry was recorded at all.
        EntityLoadState.IsKnownUnloaded(trackedArchive, "Anything").ShouldBeTrue();
    }

    [Fact]
    public async Task RecordingIsPerInstanceNotPerId()
    {
        var game = await AddGameAsync();

        using var withArchives = await ContextFactory.CreateDbContextAsync();
        var included = await withArchives.Games.Include(g => g.Archives).FirstAsync(g => g.Id == game.Id);
        EntityLoadState.Record(withArchives);

        using var without = await ContextFactory.CreateDbContextAsync();
        var omitted = await without.Games.FirstAsync(g => g.Id == game.Id);
        EntityLoadState.Record(without);

        // Two separate materializations of the same row carry their own state.
        EntityLoadState.IsKnownUnloaded(included, nameof(Game.Archives)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(omitted, nameof(Game.Archives)).ShouldBeTrue();
    }

    [Fact]
    public async Task RecordingLeavesEntityStateAndPendingChangesAlone()
    {
        var game = await AddGameAsync();

        using var context = await ContextFactory.CreateDbContextAsync();

        var loaded = await context.Games.FirstAsync(g => g.Id == game.Id);

        loaded.Description = "Pending edit";

        EntityLoadState.Record(context);

        // Recording suppresses change detection while it walks the tracker. That must not lose an
        // edit made before the walk, and must not leave detection turned off afterwards.
        context.ChangeTracker.AutoDetectChangesEnabled.ShouldBeTrue();
        context.Entry(loaded).State.ShouldBe(EntityState.Modified);

        await context.SaveChangesAsync();

        var gameService = GetService<GameService>();

        (await gameService.GetAsync(game.Id)).Description.ShouldBe("Pending edit");
    }

    [Fact]
    public async Task TheServiceLayerRecordsStateOnEveryReadPath()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        var byId = await gameService.Include(g => g.Archives).GetAsync(game.Id);
        EntityLoadState.IsKnownUnloaded(byId, nameof(Game.Archives)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(byId, nameof(Game.Tags)).ShouldBeTrue();

        var byPredicate = (await gameService.Include(g => g.Tags).GetAsync(g => g.Id == game.Id)).Single();
        EntityLoadState.IsKnownUnloaded(byPredicate, nameof(Game.Tags)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(byPredicate, nameof(Game.Archives)).ShouldBeTrue();

        gameService.Include(g => g.Scripts);
        var byFirst = await gameService.FirstAsync(g => g.Id == game.Id);
        EntityLoadState.IsKnownUnloaded(byFirst, nameof(Game.Scripts)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(byFirst, nameof(Game.Archives)).ShouldBeTrue();

        var byFirstOrDefault = await gameService.Include(g => g.Media).FirstOrDefaultAsync(g => g.Id == game.Id);
        EntityLoadState.IsKnownUnloaded(byFirstOrDefault, nameof(Game.Media)).ShouldBeFalse();
        EntityLoadState.IsKnownUnloaded(byFirstOrDefault, nameof(Game.Archives)).ShouldBeTrue();
    }
}
