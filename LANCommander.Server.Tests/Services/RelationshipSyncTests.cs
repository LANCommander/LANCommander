using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using GameAction = LANCommander.Server.Data.Models.Action;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// Saving an entity must only touch the relationships the caller actually loaded.
///
/// Game and Redistributable initialize their collection navigations to empty collections, so an
/// entity loaded by a query that omitted a navigation arrives at UpdateAsync carrying an empty —
/// but not null — collection. Treating that as "empty this relationship" silently destroyed
/// children the caller never saw: editing a game's description through an edit view that does not
/// load archives detached every archive from the game while leaving the files on disk.
/// </summary>
[Collection("Application")]
public class RelationshipSyncTests(ApplicationFixture fixture) : DalTest(fixture)
{
    #region Game — collections the edit view does not load

    [Fact]
    public async Task EditingAGameKeepsArchives()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Description = "Changed description";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Archives).GetAsync(game.Id);

        updated.Description.ShouldBe("Changed description");
        updated.Archives.ShouldContain(a => a.Id == archive.Id);
    }

    [Fact]
    public async Task EditingAGameKeepsScripts()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var script = await AddScriptAsync(game.Id);

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Notes = "Changed notes";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Scripts).GetAsync(game.Id);

        updated.Scripts.ShouldContain(s => s.Id == script.Id);
    }

    [Fact]
    public async Task EditingAGameKeepsKeys()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var key = await AddKeyAsync(game.Id);

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Description = "Changed description";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Keys).GetAsync(game.Id);

        updated.Keys.ShouldContain(k => k.Id == key.Id);
    }

    [Fact]
    public async Task EditingAGameKeepsCustomFields()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var customField = await AddCustomFieldAsync(game.Id);

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Description = "Changed description";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.CustomFields).GetAsync(game.Id);

        updated.CustomFields.ShouldContain(c => c.Id == customField.Id);
    }

    [Fact]
    public async Task EditingAGameKeepsMedia()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var media = await AddMediaAsync(game.Id);

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Description = "Changed description";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Media).GetAsync(game.Id);

        updated.Media.ShouldContain(m => m.Id == media.Id);
    }

    [Fact]
    public async Task EditingAGameKeepsActionsSavePathsAndMultiplayerModes()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var action = await AddActionAsync(game.Id);
        var savePath = await AddSavePathAsync(game.Id);
        var multiplayerMode = await AddMultiplayerModeAsync(game.Id);

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Description = "Changed description";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService
            .AsSplitQuery()
            .Include(g => g.Actions)
            .Include(g => g.SavePaths)
            .Include(g => g.MultiplayerModes)
            .GetAsync(game.Id);

        updated.Actions.ShouldContain(a => a.Id == action.Id);
        updated.SavePaths.ShouldContain(s => s.Id == savePath.Id);
        updated.MultiplayerModes.ShouldContain(m => m.Id == multiplayerMode.Id);
    }

    [Fact]
    public async Task EditingAGameKeepsManyToManyTaxonomies()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var tag = await AddTagAsync();
        var genre = await AddGenreAsync();
        var platform = await AddPlatformAsync();
        var category = await AddCategoryAsync();
        var developer = await AddCompanyAsync();
        var collection = await AddCollectionAsync();

        var seed = await gameService
            .AsSplitQuery()
            .Include(g => g.Tags)
            .Include(g => g.Genres)
            .Include(g => g.Platforms)
            .Include(g => g.Categories)
            .Include(g => g.Developers)
            .Include(g => g.Collections)
            .GetAsync(game.Id);

        seed.Tags.Add(tag);
        seed.Genres.Add(genre);
        seed.Platforms.Add(platform);
        seed.Categories.Add(category);
        seed.Developers.Add(developer);
        seed.Collections.Add(collection);

        await gameService.UpdateAsync(seed);

        // Now save again through a query that loaded none of them.
        var loaded = await gameService.Include(g => g.Actions).GetAsync(game.Id);

        loaded.Description = "Changed description";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService
            .AsSplitQuery()
            .Include(g => g.Tags)
            .Include(g => g.Genres)
            .Include(g => g.Platforms)
            .Include(g => g.Categories)
            .Include(g => g.Developers)
            .Include(g => g.Collections)
            .GetAsync(game.Id);

        updated.Tags.ShouldContain(t => t.Id == tag.Id);
        updated.Genres.ShouldContain(g => g.Id == genre.Id);
        updated.Platforms.ShouldContain(p => p.Id == platform.Id);
        updated.Categories.ShouldContain(c => c.Id == category.Id);
        updated.Developers.ShouldContain(d => d.Id == developer.Id);
        updated.Collections.ShouldContain(c => c.Id == collection.Id);
    }

    /// <summary>
    /// Mirrors the include list the general game edit view actually uses. GameService.UpdateAsync
    /// syncs twenty-one relationships while that view loads seventeen, so this pins down every
    /// collection the view leaves behind in one place.
    /// </summary>
    [Fact]
    public async Task SavingFromTheGeneralEditViewKeepsEverythingThatViewDoesNotLoad()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);
        var script = await AddScriptAsync(game.Id);
        var key = await AddKeyAsync(game.Id);
        var customField = await AddCustomFieldAsync(game.Id);

        var loaded = await gameService
            .AsSplitQuery()
            .Include(g => g.DependentGames)
            .Include(g => g.Actions)
            .Include(g => g.BaseGame)
            .Include(g => g.Categories)
            .Include(g => g.Collections)
            .Include(g => g.CreatedBy)
            .Include(g => g.Developers)
            .Include(g => g.Engine)
            .Include(g => g.ExternalIds)
            .Include(g => g.Genres)
            .Include(g => g.Media)
            .Include(g => g.MultiplayerModes)
            .Include(g => g.Platforms)
            .Include(g => g.Publishers)
            .Include(g => g.Redistributables)
            .Include(g => g.SavePaths)
            .Include(g => g.Tags)
            .Include(g => g.UpdatedBy)
            .GetAsync(game.Id);

        loaded.Description = "Edited from the general view";

        await gameService.UpdateAsync(loaded);

        var updated = await gameService
            .AsSplitQuery()
            .Include(g => g.Archives)
            .Include(g => g.Scripts)
            .Include(g => g.Keys)
            .Include(g => g.CustomFields)
            .GetAsync(game.Id);

        updated.Description.ShouldBe("Edited from the general view");
        updated.Archives.ShouldContain(a => a.Id == archive.Id);
        updated.Scripts.ShouldContain(s => s.Id == script.Id);
        updated.Keys.ShouldContain(k => k.Id == key.Id);
        updated.CustomFields.ShouldContain(c => c.Id == customField.Id);
    }

    #endregion

    #region Redistributable — the other model with initialized collections

    [Fact]
    public async Task EditingARedistributableKeepsArchivesScriptsAndGames()
    {
        var redistributableService = GetService<RedistributableService>();
        var scriptService = GetService<ScriptService>();

        var redistributable = await AddRedistributableAsync();
        var archive = await AddRedistributableArchiveAsync(redistributable.Id);
        var game = await AddGameAsync();

        var script = await scriptService.AddAsync(new Script
        {
            Name = Unique("Script"),
            Contents = "echo hello",
            Type = SDK.Enums.ScriptType.Install,
            RedistributableId = redistributable.Id,
        });

        var seed = await redistributableService.Include(r => r.Games).GetAsync(redistributable.Id);

        seed.Games.Add(game);

        await redistributableService.UpdateAsync(seed);

        // Save again through a query that loaded none of the relationships.
        var loaded = await redistributableService.GetAsync(redistributable.Id);

        loaded.Description = "Changed description";

        await redistributableService.UpdateAsync(loaded);

        var updated = await redistributableService
            .AsSplitQuery()
            .Include(r => r.Archives)
            .Include(r => r.Scripts)
            .Include(r => r.Games)
            .GetAsync(redistributable.Id);

        updated.Description.ShouldBe("Changed description");
        updated.Archives.ShouldContain(a => a.Id == archive.Id);
        updated.Scripts.ShouldContain(s => s.Id == script.Id);
        updated.Games.ShouldContain(g => g.Id == game.Id);
    }

    #endregion

    #region The fix must not swing too far the other way

    [Fact]
    public async Task ClearingALoadedCollectionStillClearsIt()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        await AddArchiveAsync(game.Id);

        var loaded = await gameService.Include(g => g.Archives).GetAsync(game.Id);

        loaded.Archives.ShouldNotBeEmpty();
        loaded.Archives.Clear();

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Archives).GetAsync(game.Id);

        updated.Archives.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemovingASingleItemFromALoadedCollectionStillSyncs()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var kept = await AddArchiveAsync(game.Id);
        var removed = await AddArchiveAsync(game.Id);

        var loaded = await gameService.Include(g => g.Archives).GetAsync(game.Id);

        loaded.Archives.Remove(loaded.Archives.First(a => a.Id == removed.Id));

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Archives).GetAsync(game.Id);

        updated.Archives.ShouldContain(a => a.Id == kept.Id);
        updated.Archives.ShouldNotContain(a => a.Id == removed.Id);
    }

    [Fact]
    public async Task AddingToAndRemovingFromALoadedManyToManyStillSyncs()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var tag = await AddTagAsync();

        var loaded = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        loaded.Tags.Add(tag);

        await gameService.UpdateAsync(loaded);

        var withTag = await gameService.Include(g => g.Tags).GetAsync(game.Id);
        withTag.Tags.ShouldContain(t => t.Id == tag.Id);

        withTag.Tags.Remove(withTag.Tags.First(t => t.Id == tag.Id));

        await gameService.UpdateAsync(withTag);

        var withoutTag = await gameService.Include(g => g.Tags).GetAsync(game.Id);
        withoutTag.Tags.ShouldNotContain(t => t.Id == tag.Id);
    }

    /// <summary>
    /// The skip is keyed on the collection being empty as well as unloaded. A caller that fetches
    /// without an include and then assigns a populated collection — the pattern the redistributable
    /// picker uses — must still have that assignment written.
    /// </summary>
    [Fact]
    public async Task AssigningToAnUnloadedCollectionStillSyncs()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var tag = await AddTagAsync();

        var loaded = await gameService.GetAsync(game.Id);

        loaded.Tags = [tag];

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        updated.Tags.ShouldContain(t => t.Id == tag.Id);
    }

    [Fact]
    public async Task EntitiesBuiltByHandStillSyncTheirCollections()
    {
        var gameService = GetService<GameService>();

        var tag = await AddTagAsync();
        var game = await AddGameAsync();

        // Entities assembled outside the query pipeline (API payloads, metadata providers) carry no
        // recorded load state and keep their previous sync-everything behavior.
        await gameService.UpdateAsync(new Game
        {
            Id = game.Id,
            Title = game.Title,
            Tags = [tag],
        });

        var updated = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        updated.Tags.ShouldContain(t => t.Id == tag.Id);
    }

    [Fact]
    public async Task ReferenceNavigationsAreStillSynced()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var engine = await AddEngineAsync();

        var loaded = await gameService.GetAsync(game.Id);

        loaded.Engine = engine;

        await gameService.UpdateAsync(loaded);

        var updated = await gameService.Include(g => g.Engine).GetAsync(game.Id);

        updated.Engine.ShouldNotBeNull();
        updated.Engine.Id.ShouldBe(engine.Id);
    }

    [Fact]
    public async Task AddAsyncStillWiresUpCollectionsOnNewEntities()
    {
        var gameService = GetService<GameService>();

        var tag = await AddTagAsync();
        var genre = await AddGenreAsync();

        var game = await gameService.AddAsync(new Game
        {
            Title = Unique("Game"),
            Tags = [tag],
            Genres = [genre],
        });

        var added = await gameService
            .AsSplitQuery()
            .Include(g => g.Tags)
            .Include(g => g.Genres)
            .GetAsync(game.Id);

        added.Tags.ShouldContain(t => t.Id == tag.Id);
        added.Genres.ShouldContain(g => g.Id == genre.Id);
    }

    #endregion

    #region Models whose collections are null until loaded were never affected — keep it that way

    [Fact]
    public async Task ServerRelationshipsStillSync()
    {
        var serverService = GetService<ServerService>();
        var actionService = GetService<ActionService>();

        var server = await AddServerAsync();

        var action = await actionService.AddAsync(new GameAction
        {
            Name = Unique("Action"),
            Path = "server.exe",
            ServerId = server.Id,
        });

        var loaded = await serverService.GetAsync(server.Id);

        loaded.Arguments = "-dedicated";

        await serverService.UpdateAsync(loaded);

        var updated = await serverService.Include(s => s.Actions).GetAsync(server.Id);

        updated.Arguments.ShouldBe("-dedicated");
        updated.Actions.ShouldContain(a => a.Id == action.Id);

        // And an explicit clear on a loaded collection is still honored.
        updated.Actions.Clear();

        await serverService.UpdateAsync(updated);

        var cleared = await serverService.Include(s => s.Actions).GetAsync(server.Id);

        cleared.Actions.ShouldBeEmpty();
    }

    [Fact]
    public async Task StorageLocationChildrenSurviveAnUnrelatedEdit()
    {
        var storageLocationService = GetService<StorageLocationService>();
        var archiveService = GetService<ArchiveService>();

        var storageLocation = await storageLocationService.AddAsync(new StorageLocation
        {
            Path = Unique("Storage"),
            Type = SDK.Enums.StorageLocationType.Archive,
        });

        var game = await AddGameAsync();

        var archive = await archiveService.AddAsync(new Archive
        {
            GameId = game.Id,
            ObjectKey = Unique("object"),
            Version = "1.0",
            StorageLocationId = storageLocation.Id,
        });

        var loaded = await storageLocationService.GetAsync(storageLocation.Id);

        loaded.Path = Unique("Storage");

        await storageLocationService.UpdateAsync(loaded);

        var stillLinked = await archiveService.GetAsync(archive.Id);

        stillLinked.ShouldNotBeNull();
        stillLinked.StorageLocationId.ShouldBe(storageLocation.Id);
    }

    #endregion
}
