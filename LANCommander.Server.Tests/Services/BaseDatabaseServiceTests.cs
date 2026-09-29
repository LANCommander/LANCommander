using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SortDirection = LANCommander.Server.Data.Enums.SortDirection;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Covers the query surface of BaseDatabaseService. Recording which navigations a query loaded
/// happens inside these methods, between materialization and the modifier reset, so every read
/// path, every query modifier and the modifier lifecycle itself are exercised here.
/// </summary>
[Collection("Application")]
public class BaseDatabaseServiceTests(ApplicationFixture fixture) : DalTest(fixture)
{
    #region Reads

    [Fact]
    public async Task GetAsyncByIdReturnsTheEntity()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        var found = await gameService.GetAsync(game.Id);

        found.ShouldNotBeNull();
        found.Id.ShouldBe(game.Id);
        found.Title.ShouldBe(game.Title);
    }

    [Fact]
    public async Task GetAsyncByIdReturnsNullWhenMissing()
    {
        var gameService = GetService<GameService>();

        (await gameService.GetAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task GetAsyncByPredicateFilters()
    {
        var gameService = GetService<GameService>();

        var title = Unique("Game");
        var game = await AddGameAsync(title);
        await AddGameAsync();

        var found = await gameService.GetAsync(g => g.Title == title);

        found.Count.ShouldBe(1);
        found.First().Id.ShouldBe(game.Id);
    }

    [Fact]
    public async Task GetAsyncWithoutPredicateReturnsEverything()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        var all = await gameService.GetAsync();

        all.ShouldContain(g => g.Id == game.Id);
    }

    [Fact]
    public async Task FirstOrDefaultAsyncReturnsNullWhenNothingMatches()
    {
        var gameService = GetService<GameService>();

        (await gameService.FirstOrDefaultAsync(g => g.Title == Unique("Nothing"))).ShouldBeNull();
    }

    [Fact]
    public async Task FirstAsyncReturnsTheMatchAndThrowsWhenThereIsNone()
    {
        var gameService = GetService<GameService>();

        var title = Unique("Game");
        var game = await AddGameAsync(title);

        (await gameService.FirstAsync(g => g.Title == title)).Id.ShouldBe(game.Id);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await gameService.FirstAsync(g => g.Title == Unique("Nothing")));
    }

    [Fact]
    public async Task AnyAsyncAndExistsAsyncAgreeWithTheData()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        (await gameService.AnyAsync()).ShouldBeTrue();
        (await gameService.ExistsAsync(game.Id)).ShouldBeTrue();
        (await gameService.ExistsAsync(Guid.NewGuid())).ShouldBeFalse();
        (await gameService.ExistsAsync(g => g.Title == game.Title)).ShouldBeTrue();
        (await gameService.ExistsAsync(g => g.Title == Unique("Nothing"))).ShouldBeFalse();
    }

    /// <summary>
    /// GetAsync&lt;U&gt;(id) maps in memory while the predicate overloads project in the query, so both
    /// halves are covered. The entity types here are the ones the API endpoints actually project.
    /// </summary>
    [Fact]
    public async Task ProjectionOverloadsStillMap()
    {
        var tagService = GetService<TagService>();

        var name = Unique("Tag");
        var tag = await tagService.AddAsync(new Tag { Name = name });

        var single = await tagService.GetAsync<SDK.Models.Tag>(tag.Id);
        single.ShouldNotBeNull();
        single.Id.ShouldBe(tag.Id);
        single.Name.ShouldBe(name);

        var many = await tagService.GetAsync<SDK.Models.Tag>(t => t.Name == name);
        many.Count.ShouldBe(1);
        many.First().Id.ShouldBe(tag.Id);

        var first = await tagService.FirstAsync<SDK.Models.Tag>(t => t.Name == name);
        first.Id.ShouldBe(tag.Id);

        var firstOrDefault = await tagService.FirstOrDefaultAsync<SDK.Models.Tag>(t => t.Name == name);
        firstOrDefault.Id.ShouldBe(tag.Id);

        var all = await tagService.GetAsync<SDK.Models.Tag>();
        all.ShouldContain(t => t.Id == tag.Id);
    }

    [Fact]
    public async Task ProjectionOverloadsMapTheTypesTheApiActuallyReturns()
    {
        var archiveService = GetService<ArchiveService>();
        var scriptService = GetService<ScriptService>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);
        var script = await AddScriptAsync(game.Id);

        var archives = await archiveService.GetAsync<SDK.Models.Archive>(a => a.Id == archive.Id);
        archives.Count.ShouldBe(1);
        archives.First().Id.ShouldBe(archive.Id);

        (await archiveService.GetAsync<SDK.Models.Archive>(archive.Id)).Id.ShouldBe(archive.Id);

        var scripts = await scriptService.GetAsync<SDK.Models.Script>(s => s.Id == script.Id);
        scripts.Count.ShouldBe(1);
        scripts.First().Id.ShouldBe(script.Id);
    }

    #endregion

    #region Query modifiers

    [Fact]
    public async Task IncludeByExpressionPopulatesTheNavigationAndOmittingItDoesNot()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        await AddArchiveAsync(game.Id);

        var included = await gameService.Include(g => g.Archives).GetAsync(game.Id);
        included.Archives.ShouldNotBeEmpty();

        var omitted = await gameService.GetAsync(game.Id);
        omitted.Archives.ShouldBeEmpty();
    }

    /// <summary>
    /// Both string overloads are currently unused by production code. The params overload used to
    /// call itself — a string[] argument binds to it in preference to the IEnumerable overload — so
    /// the first caller to reach it would have taken down the process with a stack overflow.
    /// </summary>
    [Fact]
    public async Task IncludeByNamePopulatesTheNavigation()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        await AddArchiveAsync(game.Id);

        var viaParams = await gameService.Include(nameof(Game.Archives)).GetAsync(game.Id);
        viaParams.Archives.ShouldNotBeEmpty();

        var viaArray = await gameService.Include(new[] { nameof(Game.Archives) }).GetAsync(game.Id);
        viaArray.Archives.ShouldNotBeEmpty();

        var viaEnumerable = await gameService
            .Include(new List<string> { nameof(Game.Archives) })
            .GetAsync(game.Id);
        viaEnumerable.Archives.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task QueryModifierPopulatesTheNavigation()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        await AddArchiveAsync(game.Id);

        var included = await gameService
            .Query(q => q.Include(g => g.Archives))
            .GetAsync(game.Id);

        included.Archives.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task AsNoTrackingStillReturnsIncludedData()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);

        var loaded = await gameService
            .AsNoTracking()
            .Include(g => g.Archives)
            .GetAsync(game.Id);

        loaded.Archives.ShouldContain(a => a.Id == archive.Id);
    }

    [Fact]
    public async Task AsSplitQueryStillReturnsIncludedData()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);
        var tag = await AddTagAsync();

        var seed = await gameService.Include(g => g.Tags).GetAsync(game.Id);
        seed.Tags.Add(tag);
        await gameService.UpdateAsync(seed);

        var loaded = await gameService
            .AsSplitQuery()
            .Include(g => g.Archives)
            .Include(g => g.Tags)
            .GetAsync(game.Id);

        loaded.Archives.ShouldContain(a => a.Id == archive.Id);
        loaded.Tags.ShouldContain(t => t.Id == tag.Id);
    }

    [Fact]
    public async Task SortByOrdersInBothDirections()
    {
        var tagService = GetService<TagService>();

        var prefix = Guid.NewGuid().ToString("N");

        await tagService.AddAsync(new Tag { Name = $"{prefix}-c" });
        await tagService.AddAsync(new Tag { Name = $"{prefix}-a" });
        await tagService.AddAsync(new Tag { Name = $"{prefix}-b" });

        var ascending = await tagService
            .SortBy(t => t.Name)
            .GetAsync(t => t.Name.StartsWith(prefix));

        ascending.Select(t => t.Name).ShouldBe([$"{prefix}-a", $"{prefix}-b", $"{prefix}-c"]);

        var descending = await tagService
            .SortBy(t => t.Name, SortDirection.Descending)
            .GetAsync(t => t.Name.StartsWith(prefix));

        descending.Select(t => t.Name).ShouldBe([$"{prefix}-c", $"{prefix}-b", $"{prefix}-a"]);
    }

    #endregion

    #region Modifier lifecycle

    [Fact]
    public async Task ModifiersDoNotLeakIntoTheNextQuery()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        await AddArchiveAsync(game.Id);

        var included = await gameService.Include(g => g.Archives).GetAsync(game.Id);
        included.Archives.ShouldNotBeEmpty();

        // Modifiers are cleared in a finally block after each call. A leak here would silently
        // change every later query on the same scoped service instance.
        var plain = await gameService.GetAsync(game.Id);
        plain.Archives.ShouldBeEmpty();
    }

    [Fact]
    public async Task ModifiersAreClearedEvenWhenTheQueryThrows()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        await AddArchiveAsync(game.Id);

        // Include mutates and returns the same service instance, so stage the modifier first;
        // FirstAsync is only exposed on the concrete service, not on IBaseDatabaseService.
        gameService.Include(g => g.Archives);

        await Should.ThrowAsync<InvalidOperationException>(
            async () => await gameService.FirstAsync(g => g.Title == Unique("Nothing")));

        var plain = await gameService.GetAsync(game.Id);
        plain.Archives.ShouldBeEmpty();
    }

    #endregion

    #region Writes

    [Fact]
    public async Task AddMissingAsyncCreatesOnceAndThenReportsTheExistingEntity()
    {
        var tagService = GetService<TagService>();

        var name = Unique("Tag");

        var created = await tagService.AddMissingAsync(t => t.Name == name, new Tag { Name = name });

        created.Existing.ShouldBeFalse();
        created.Value.Name.ShouldBe(name);

        var second = await tagService.AddMissingAsync(t => t.Name == name, new Tag { Name = name });

        second.Existing.ShouldBeTrue();
        second.Value.Id.ShouldBe(created.Value.Id);
    }

    [Fact]
    public async Task AddAsyncStampsCreatedOnAndUpdateAsyncStampsUpdatedOn()
    {
        var tagService = GetService<TagService>();

        var tag = await tagService.AddAsync(new Tag { Name = Unique("Tag") });

        tag.CreatedOn.ShouldNotBe(default);

        var loaded = await tagService.GetAsync(tag.Id);

        loaded.Name = Unique("Tag");

        await tagService.UpdateAsync(loaded);

        var updated = await tagService.GetAsync(tag.Id);

        updated.UpdatedOn.ShouldBeGreaterThan(default(DateTime));
        updated.Name.ShouldBe(loaded.Name);
    }

    [Fact]
    public async Task DeleteAsyncRemovesTheEntity()
    {
        var tagService = GetService<TagService>();

        var tag = await tagService.AddAsync(new Tag { Name = Unique("Tag") });

        await tagService.DeleteAsync(tag);

        (await tagService.GetAsync(tag.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task DeleteRangeAsyncRemovesEveryEntity()
    {
        var tagService = GetService<TagService>();

        var prefix = Guid.NewGuid().ToString("N");

        var first = await tagService.AddAsync(new Tag { Name = $"{prefix}-a" });
        var second = await tagService.AddAsync(new Tag { Name = $"{prefix}-b" });

        await tagService.DeleteRangeAsync([first, second]);

        (await tagService.GetAsync(t => t.Name.StartsWith(prefix))).ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateAsyncPersistsScalarChanges()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        var loaded = await gameService.GetAsync(game.Id);

        loaded.Description = "Updated";
        loaded.Notes = "Noted";
        loaded.SortTitle = "Sorted";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.GetAsync(game.Id);

        updated.Description.ShouldBe("Updated");
        updated.Notes.ShouldBe("Noted");
        updated.SortTitle.ShouldBe("Sorted");
    }

    #endregion
}
