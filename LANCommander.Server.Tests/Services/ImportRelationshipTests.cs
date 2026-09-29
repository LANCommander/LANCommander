using LANCommander.SDK.Enums;
using LANCommander.Server.ImportExport.Factories;
using LANCommander.Server.Services;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

/// <summary>
/// The import path loads a game without includes, rewrites scalars and then re-syncs taxonomies
/// through SyncRelatedCollectionAsync. It is the main server-side consumer of the update path that
/// is not a Blazor page, so it gets its own coverage of what a re-import may and may not destroy.
/// </summary>
[Collection("Application")]
public class ImportRelationshipTests(ApplicationFixture fixture) : DalTest(fixture)
{
    SDK.Models.Manifest.Game BuildManifest(Guid id, string title) => new()
    {
        Id = id,
        Title = title,
        Description = "Imported",
        Type = GameType.MainGame,
    };

    async Task ImportAsync(SDK.Models.Manifest.Game manifest)
    {
        var importContext = GetService<ImportContextFactory>().Create();

        await importContext.InitializeMetadataUpdateAsync(manifest);
        await importContext.ImportQueueAsync();
    }

    [Fact]
    public async Task ReImportingAGameDoesNotDestroyItsUploadedContent()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();
        var archive = await AddArchiveAsync(game.Id);
        var media = await AddMediaAsync(game.Id);
        var script = await AddScriptAsync(game.Id);
        var key = await AddKeyAsync(game.Id);

        await ImportAsync(BuildManifest(game.Id, game.Title));

        var updated = await gameService
            .AsSplitQuery()
            .Include(g => g.Archives)
            .Include(g => g.Media)
            .Include(g => g.Scripts)
            .Include(g => g.Keys)
            .GetAsync(game.Id);

        updated.Description.ShouldBe("Imported");
        updated.Archives.ShouldContain(a => a.Id == archive.Id);
        updated.Media.ShouldContain(m => m.Id == media.Id);
        updated.Scripts.ShouldContain(s => s.Id == script.Id);
        updated.Keys.ShouldContain(k => k.Id == key.Id);
    }

    [Fact]
    public async Task ImportingTaxonomiesStillReplacesThem()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        var withTags = BuildManifest(game.Id, game.Title);
        withTags.Tags = [new SDK.Models.Manifest.Tag { Name = Unique("Tag") }];
        withTags.Genres = [new SDK.Models.Manifest.Genre { Name = Unique("Genre") }];

        await ImportAsync(withTags);

        var tagged = await gameService
            .AsSplitQuery()
            .Include(g => g.Tags)
            .Include(g => g.Genres)
            .GetAsync(game.Id);

        tagged.Tags.ShouldContain(t => t.Name == withTags.Tags.First().Name);
        tagged.Genres.ShouldContain(g => g.Name == withTags.Genres.First().Name);

        // SyncRelatedCollectionAsync is driven by an explicit record list rather than by the state
        // of the entity's navigation, so a manifest that drops a tag still removes it.
        var replaced = BuildManifest(game.Id, game.Title);
        replaced.Tags = [new SDK.Models.Manifest.Tag { Name = Unique("Tag") }];

        await ImportAsync(replaced);

        var retagged = await gameService.Include(g => g.Tags).GetAsync(game.Id);

        retagged.Tags.ShouldContain(t => t.Name == replaced.Tags.First().Name);
        retagged.Tags.ShouldNotContain(t => t.Name == withTags.Tags.First().Name);
    }

    [Fact]
    public async Task ImportingReplacesExternalIdsIncludingClearingThem()
    {
        var gameService = GetService<GameService>();

        var game = await AddGameAsync();

        var withIds = BuildManifest(game.Id, game.Title);
        withIds.ExternalIds =
        [
            new SDK.Models.Manifest.GameExternalId { Provider = "IGDB", ExternalId = "1234" },
        ];

        await ImportAsync(withIds);

        var identified = await gameService.Include(g => g.ExternalIds).GetAsync(game.Id);
        identified.ExternalIds.ShouldContain(e => e.ExternalId == "1234");

        // The importer assigns external IDs wholesale from the manifest, so an empty manifest set
        // must clear them. That only holds because the importer loads the navigation it replaces.
        var withoutIds = BuildManifest(game.Id, game.Title);
        withoutIds.ExternalIds = [];

        await ImportAsync(withoutIds);

        var cleared = await gameService.Include(g => g.ExternalIds).GetAsync(game.Id);
        cleared.ExternalIds.ShouldBeEmpty();
    }
}
