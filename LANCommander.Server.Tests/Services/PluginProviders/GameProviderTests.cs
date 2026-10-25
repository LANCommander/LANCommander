using LANCommander.SDK.Enums;
using LANCommander.Server.Plugins;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services.PluginProviders;

[Collection("Application")]
public class GameProviderTests(ApplicationFixture fixture) : BaseTest(fixture)
{
    IGameProvider Games => GetService<IGameProvider>();
    ITaxonomyProvider Taxonomy => GetService<ITaxonomyProvider>();

    Task<GameInfo> AddGameAsync(string title = "Plugin game") =>
        Games.AddAsync(new GameInfo { Title = $"{title} {Guid.NewGuid():N}" });

    [Fact]
    public async Task AGameCanBeAddedUpdatedAndDeleted()
    {
        var engine = await Taxonomy.GetOrAddAsync(TaxonomyKind.Engine, $"Engine {Guid.NewGuid():N}");
        var added = await Games.AddAsync(new GameInfo
        {
            Title = "  Plugin Round Trip  ",
            Description = "Added by a plugin",
            ReleasedOn = new DateTime(1999, 3, 31),
            Singleplayer = true,
            EngineId = engine.Id,
        });

        added.Id.ShouldNotBe(Guid.Empty);
        added.Title.ShouldBe("Plugin Round Trip");
        added.EngineId.ShouldBe(engine.Id);

        var updated = await Games.UpdateAsync(added with { Notes = "Updated", Singleplayer = false, EngineId = null });

        updated.Notes.ShouldBe("Updated");
        updated.Singleplayer.ShouldBeFalse();
        updated.EngineId.ShouldBeNull();
        updated.Description.ShouldBe("Added by a plugin");

        await Games.DeleteAsync(added.Id);

        (await Games.GetAsync(added.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task AnAddonListsUnderItsBaseGame()
    {
        var game = await AddGameAsync();
        var addon = await Games.AddAsync(new GameInfo { Title = "Plugin addon", Type = GameType.Expansion, BaseGameId = game.Id });

        (await Games.GetAddonsAsync(game.Id)).ShouldContain(found => found.Id == addon.Id);
    }

    [Fact]
    public async Task AMissingBaseGameIsRefused() =>
        await Should.ThrowAsync<KeyNotFoundException>(() =>
            Games.AddAsync(new GameInfo { Title = "Orphan", BaseGameId = Guid.NewGuid() }));

    [Fact]
    public async Task LinksAreReplacedAndClearedOneKindAtATime()
    {
        var game = await AddGameAsync();
        var action = await Taxonomy.GetOrAddAsync(TaxonomyKind.Genre, $"Action {Guid.NewGuid():N}");
        var puzzle = await Taxonomy.GetOrAddAsync(TaxonomyKind.Genre, $"Puzzle {Guid.NewGuid():N}");
        var tag = await Taxonomy.GetOrAddAsync(TaxonomyKind.Tag, $"Tag {Guid.NewGuid():N}");
        var studio = await Taxonomy.GetOrAddAsync(TaxonomyKind.Company, $"Studio {Guid.NewGuid():N}");

        await Games.SetLinksAsync(game.Id, GameLinkKind.Genres, [action.Id, puzzle.Id]);
        await Games.SetLinksAsync(game.Id, GameLinkKind.Tags, [tag.Id]);
        await Games.SetLinksAsync(game.Id, GameLinkKind.Developers, [studio.Id]);

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Genres)).ShouldBe([action.Id, puzzle.Id], ignoreOrder: true);

        // Neither a scalar update nor another kind's links touch them
        await Games.UpdateAsync((await Games.GetAsync(game.Id))! with { Notes = "Links stay" });
        await Games.SetLinksAsync(game.Id, GameLinkKind.Genres, [puzzle.Id]);

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Genres)).ShouldBe([puzzle.Id]);
        (await Games.GetLinksAsync(game.Id, GameLinkKind.Tags)).ShouldBe([tag.Id]);
        (await Games.GetLinksAsync(game.Id, GameLinkKind.Developers)).ShouldBe([studio.Id]);
        (await Games.GetLinksAsync(game.Id, GameLinkKind.Publishers)).ShouldBeEmpty();

        await Games.SetLinksAsync(game.Id, GameLinkKind.Genres, []);

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Genres)).ShouldBeEmpty();
        (await Games.GetLinksAsync(game.Id, GameLinkKind.Tags)).ShouldBe([tag.Id]);
    }

    [Fact]
    public async Task ALinkToAMissingRecordIsRefusedAndChangesNothing()
    {
        var game = await AddGameAsync();
        var tag = await Taxonomy.GetOrAddAsync(TaxonomyKind.Tag, $"Tag {Guid.NewGuid():N}");

        await Games.SetLinksAsync(game.Id, GameLinkKind.Tags, [tag.Id]);

        await Should.ThrowAsync<KeyNotFoundException>(() => Games.SetLinksAsync(game.Id, GameLinkKind.Tags, [tag.Id, Guid.NewGuid()]));

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Tags)).ShouldBe([tag.Id]);
    }

    [Fact]
    public async Task RedistributablesAreLinkedToTheLatestVersionToo()
    {
        var game = await AddGameAsync();
        var redistributable = await GetService<IRedistributableProvider>().AddAsync(new RedistributableInfo { Name = $"Runtime {Guid.NewGuid():N}" });

        await Games.SetLinksAsync(game.Id, GameLinkKind.Redistributables, [redistributable.Id]);
        await Games.SetRedistributableOptionsAsync(game.Id, redistributable.Id, """{"Mode":"Silent"}""");

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Redistributables)).ShouldBe([redistributable.Id]);

        var versions = GetService<GameVersionService>();
        var latest = (await versions.GetLatestIdAsync(game.Id))!.Value;

        (await versions.GetRedistributablesAsync(latest)).Select(link => link.RedistributableId).ShouldBe([redistributable.Id]);
        (await Games.GetRedistributableOptionsAsync(game.Id, redistributable.Id)).ShouldBe("""{"Mode":"Silent"}""");
    }

    [Fact]
    public async Task ToolsCanBeLinkedFromEitherSide()
    {
        var game = await AddGameAsync();
        var tools = GetService<IToolProvider>();
        var fromGame = await tools.AddAsync(new ToolInfo { Name = $"From game {Guid.NewGuid():N}" });
        var fromTool = await tools.AddAsync(new ToolInfo { Name = $"From tool {Guid.NewGuid():N}" });

        await Games.SetLinksAsync(game.Id, GameLinkKind.Tools, [fromGame.Id]);
        await tools.SetGamesAsync(fromTool.Id, [game.Id]);

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Tools)).ShouldBe([fromGame.Id, fromTool.Id], ignoreOrder: true);
        (await tools.GetGamesAsync(fromGame.Id)).ShouldBe([game.Id]);

        await tools.SetGamesAsync(fromTool.Id, []);

        (await Games.GetLinksAsync(game.Id, GameLinkKind.Tools)).ShouldBe([fromGame.Id]);
    }

    [Fact]
    public async Task AGameIsFoundByTheIdItHasInAnotherService()
    {
        var game = await AddGameAsync();
        var storeId = Guid.NewGuid().ToString("N");

        await Games.SetExternalIdAsync(game.Id, "PluginStore", "first");
        await Games.SetExternalIdAsync(game.Id, "pluginstore", storeId);

        // One id per service: the second call replaced the first
        (await Games.GetExternalIdsAsync(game.Id)).ShouldHaveSingleItem().ExternalId.ShouldBe(storeId);
        (await Games.FindByExternalIdAsync("PLUGINSTORE", storeId))!.Id.ShouldBe(game.Id);

        await Games.RemoveExternalIdAsync(game.Id, "PluginStore");

        (await Games.FindByExternalIdAsync("PluginStore", storeId)).ShouldBeNull();
    }

    [Fact]
    public async Task CustomFieldsAreSetReplacedAndRemoved()
    {
        var game = await AddGameAsync();

        await Games.SetCustomFieldAsync(game.Id, "Source", "one");
        await Games.SetCustomFieldAsync(game.Id, "Source", "two");

        (await Games.GetCustomFieldsAsync(game.Id)).ShouldHaveSingleItem().Value.ShouldBe("two");

        await Games.RemoveCustomFieldAsync(game.Id, "Source");

        (await Games.GetCustomFieldsAsync(game.Id)).ShouldBeEmpty();
    }
}
