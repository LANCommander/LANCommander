using System.Security.Claims;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Endpoints;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using ZiggyCreatures.Caching.Fusion;
using GameAction = LANCommander.Server.Data.Models.Action;

namespace LANCommander.Server.Tests.Data;

/// <summary>
/// A version is a locked snapshot of a game's config: its actions, scripts, save paths,
/// redistributables (with their option values) and option schema. Older versions keep theirs when newer
/// versions change, and the game's own schema and redistributables mirror the latest version.
/// </summary>
[Collection("Application")]
public class VersionConfigTests(ApplicationFixture fixture) : DalTest(fixture)
{
    private async Task<Game> GetGameAsync(Guid gameId)
    {
        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        return await context.Games.AsNoTracking().Include(g => g.Redistributables).FirstAsync(g => g.Id == gameId);
    }

    private async Task<string?> GetGameRedistributableOptionsAsync(Guid gameId, Guid redistributableId)
    {
        await using var context = await GetService<IDbContextFactory<DatabaseContext>>().CreateDbContextAsync();

        var row = await context.Set<Dictionary<string, object>>("GameRedistributable")
            .FirstOrDefaultAsync(e => EF.Property<Guid>(e, "GameId") == gameId && EF.Property<Guid>(e, "RedistributableId") == redistributableId);

        return row?["Options"] as string;
    }

    /// <summary>A game whose first version has redistributable A (with options) and schema S1.</summary>
    private async Task<(Game Game, GameVersion V1, Redistributable A)> SeedFirstVersionAsync()
    {
        var versionService = GetService<GameVersionService>();

        var game = await AddGameAsync();
        var a = await AddRedistributableAsync();
        var v1 = await versionService.CreateAsync(game.Id, "1.0");

        await versionService.SetOptionSchemaAsync(v1.Id, "S1");
        await versionService.SetRedistributablesAsync(v1.Id, [a.Id]);
        await versionService.SetRedistributableOptionsAsync(v1.Id, a.Id, "{\"Mode\":\"x\"}");

        return (game, v1, a);
    }

    [Fact]
    public async Task EditingLatestVersionMirrorsToTheGame()
    {
        var (game, _, a) = await SeedFirstVersionAsync();

        var mirrored = await GetGameAsync(game.Id);

        mirrored.OptionSchema.ShouldBe("S1");
        mirrored.Redistributables!.Select(r => r.Id).ShouldBe([a.Id]);
        (await GetGameRedistributableOptionsAsync(game.Id, a.Id)).ShouldBe("{\"Mode\":\"x\"}");
    }

    [Fact]
    public async Task NewVersionCopiesSchemaAndRedistributablesWithOptions()
    {
        var versionService = GetService<GameVersionService>();
        var (game, _, a) = await SeedFirstVersionAsync();

        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        (await versionService.GetOptionSchemaAsync(v2.Id)).ShouldBe("S1");
        (await versionService.GetRedistributablesAsync(v2.Id)).Select(r => r.RedistributableId).ShouldBe([a.Id]);
        (await versionService.GetRedistributableOptionsAsync(v2.Id, a.Id)).ShouldBe("{\"Mode\":\"x\"}");
    }

    [Fact]
    public async Task OlderVersionKeepsItsConfigWhenTheLatestChanges()
    {
        var versionService = GetService<GameVersionService>();
        var (game, v1, a) = await SeedFirstVersionAsync();
        var b = await AddRedistributableAsync();

        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        await versionService.SetPublishedAsync(v2.Id, true);
        await versionService.SetOptionSchemaAsync(v2.Id, "S2");
        await versionService.SetRedistributablesAsync(v2.Id, [b.Id]);

        (await versionService.GetOptionSchemaAsync(v1.Id)).ShouldBe("S1");
        (await versionService.GetRedistributablesAsync(v1.Id)).Select(r => r.RedistributableId).ShouldBe([a.Id]);
        (await versionService.GetRedistributableOptionsAsync(v1.Id, a.Id)).ShouldBe("{\"Mode\":\"x\"}");

        var mirrored = await GetGameAsync(game.Id);

        mirrored.OptionSchema.ShouldBe("S2");
        mirrored.Redistributables!.Select(r => r.Id).ShouldBe([b.Id]);
    }

    [Fact]
    public async Task EditingAnOlderVersionLeavesTheLatestAndTheGameAlone()
    {
        var versionService = GetService<GameVersionService>();
        var (game, v1, a) = await SeedFirstVersionAsync();
        var b = await AddRedistributableAsync();

        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        await versionService.SetPublishedAsync(v2.Id, true);
        await versionService.SetOptionSchemaAsync(v1.Id, "S1-fixed");
        await versionService.SetRedistributablesAsync(v1.Id, [a.Id, b.Id]);

        (await versionService.GetOptionSchemaAsync(v2.Id)).ShouldBe("S1");
        (await versionService.GetRedistributablesAsync(v2.Id)).Select(r => r.RedistributableId).ShouldBe([a.Id]);

        var mirrored = await GetGameAsync(game.Id);

        mirrored.OptionSchema.ShouldBe("S1");
        mirrored.Redistributables!.Select(r => r.Id).ShouldBe([a.Id]);
    }

    [Fact]
    public async Task ManifestForAnOlderVersionCarriesThatVersionsConfig()
    {
        var versionService = GetService<GameVersionService>();
        var gameService = GetService<GameService>();
        var (game, v1, a) = await SeedFirstVersionAsync();
        var b = await AddRedistributableAsync();

        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        await versionService.SetPublishedAsync(v2.Id, true);
        await versionService.SetOptionSchemaAsync(v2.Id, "S2");
        await versionService.SetRedistributablesAsync(v2.Id, [b.Id]);

        var old = await gameService.GetManifestAsync(game.Id, v1.Id);

        old.VersionId.ShouldBe(v1.Id);
        old.OptionSchema.ShouldBe("S1");
        old.Redistributables.Select(r => r.Id).ShouldBe([a.Id]);
        old.Redistributables.Single().Options["Mode"].ShouldBe("x");

        var latest = await gameService.GetManifestAsync(game.Id);

        latest!.VersionId.ShouldBe(v2.Id);
        latest.OptionSchema.ShouldBe("S2");
        latest.Redistributables.Select(r => r.Id).ShouldBe([b.Id]);
    }

    [Fact]
    public async Task BackfillGivesNewVersionsTheGamesSchemaAndRedistributables()
    {
        var contextFactory = GetService<IDbContextFactory<DatabaseContext>>();
        var redistributable = await AddRedistributableAsync();

        var gameId = Guid.NewGuid();

        await using (var context = await contextFactory.CreateDbContextAsync())
        {
            var game = new Game { Id = gameId, Title = Unique("Game"), OptionSchema = "Legacy", CreatedOn = DateTime.UtcNow };

            game.Redistributables = [await context.Set<Redistributable>().FirstAsync(r => r.Id == redistributable.Id)];

            context.Games.Add(game);

            await context.SaveChangesAsync();
        }

        await GameVersionBackfill.RunAsync(contextFactory, NullLogger.Instance);

        var versionService = GetService<GameVersionService>();
        var version = (await versionService.GetAllAsync(gameId)).Single();

        (await versionService.GetOptionSchemaAsync(version.Id)).ShouldBe("Legacy");
        (await versionService.GetRedistributablesAsync(version.Id)).Select(r => r.RedistributableId).ShouldBe([redistributable.Id]);
    }

    private async Task<(Game Game, GameVersion V1, GameVersion V2)> SeedVersionedActionsAsync()
    {
        var versionService = GetService<GameVersionService>();
        var actionService = GetService<ActionService>();

        var game = await AddGameAsync();
        var v1 = await versionService.CreateAsync(game.Id, "1.0");

        await actionService.AddAsync(new GameAction { Name = "Play", Path = "game.exe", GameId = game.Id, GameVersionId = v1.Id, OptionOverrides = "{}" });

        // v2 copies Play and adds its own action
        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        await actionService.AddAsync(new GameAction { Name = "Editor", Path = "editor.exe", GameId = game.Id, GameVersionId = v2.Id });
        await versionService.SetPublishedAsync(v2.Id, true);

        return (game, v1, v2);
    }

    private async Task<List<SDK.Models.Action>> GetActionsAsync(Guid gameId, string? versionIds)
    {
        var result = await GameEndpoints.GetActionsByIdAsync(
            GetService<UserService>(),
            GetService<GameService>(),
            GetService<LibraryService>(),
            GetService<SettingsProvider<Settings.Settings>>(),
            GetService<IFusionCache>(),
            GetService<SdkMapper>(),
            NullLogger<Game>.Instance,
            GetService<GameVersionService>(),
            new ClaimsPrincipal(),
            gameId,
            versionIds);

        return result.ShouldBeOfType<Ok<IEnumerable<SDK.Models.Action>>>().Value!.ToList();
    }

    [Fact]
    public async Task ActionsDefaultToTheLatestVersion()
    {
        var (game, _, _) = await SeedVersionedActionsAsync();

        (await GetActionsAsync(game.Id, null)).Select(a => a.Name).OrderBy(n => n).ShouldBe(["Editor", "Play"]);
    }

    [Fact]
    public async Task ActionsFollowTheRequestedVersion()
    {
        var (game, v1, _) = await SeedVersionedActionsAsync();

        var actions = await GetActionsAsync(game.Id, v1.Id.ToString());

        actions.Select(a => a.Name).ShouldBe(["Play"]);
        actions.Single().OptionOverrides.ShouldBe("{}");
    }

    [Fact]
    public async Task GameByIdDescribesOnlyTheLatestVersionsActions()
    {
        var (game, _, v2) = await SeedVersionedActionsAsync();

        var result = await GameEndpoints.GetByIdAsync(
            GetService<GameService>(),
            GetService<IFusionCache>(),
            GetService<ManifestMapper>(),
            game.Id);

        var manifest = result.ShouldBeOfType<Ok<SDK.Models.Manifest.Game>>().Value!;

        manifest.VersionId.ShouldBe(v2.Id);
        manifest.Actions.Select(a => a.Name).OrderBy(n => n).ShouldBe(["Editor", "Play"]);
    }

    [Fact]
    public async Task CreatingAVersionPublishesTheCurrentDraft()
    {
        var versionService = GetService<GameVersionService>();
        var game = await AddGameAsync();

        // A published game's first version is published; later ones start as drafts
        var v1 = await versionService.CreateAsync(game.Id, "1.0");
        var v2 = await versionService.CreateAsync(game.Id, "2.0");

        (await versionService.GetWithConfigAsync(v1.Id))!.Published.ShouldBeTrue();
        (await versionService.GetWithConfigAsync(v2.Id))!.Published.ShouldBeFalse();

        var v3 = await versionService.CreateAsync(game.Id, "3.0");

        (await versionService.GetWithConfigAsync(v2.Id))!.Published.ShouldBeTrue();
        (await versionService.GetWithConfigAsync(v3.Id))!.Published.ShouldBeFalse();
    }

    [Fact]
    public async Task LaunchersAreOfferedTheNewestPublishedVersion()
    {
        var versionService = GetService<GameVersionService>();
        var gameService = GetService<GameService>();
        var (game, v1, _) = await SeedFirstVersionAsync();

        // v1 is published by creating v2, which stays a draft with its own schema
        var v2 = await versionService.CreateAsync(game.Id, "2.0");
        await versionService.SetOptionSchemaAsync(v2.Id, "S2-draft");

        (await versionService.GetCurrentIdAsync(game.Id)).ShouldBe(v1.Id);
        (await gameService.GetManifestAsync(game.Id))!.OptionSchema.ShouldBe("S1");
        (await gameService.HasUpdateAsync(game.Id, v1.Id, "1.0")).ShouldBeFalse();
        (await GetGameAsync(game.Id)).OptionSchema.ShouldBe("S1");

        await versionService.SetPublishedAsync(v2.Id, true);

        (await versionService.GetCurrentIdAsync(game.Id)).ShouldBe(v2.Id);
        (await gameService.GetManifestAsync(game.Id))!.OptionSchema.ShouldBe("S2-draft");
        (await gameService.HasUpdateAsync(game.Id, v1.Id, "1.0")).ShouldBeTrue();
        (await GetGameAsync(game.Id)).OptionSchema.ShouldBe("S2-draft");

        // Unpublishing goes back to the newest published version before it
        await versionService.SetPublishedAsync(v2.Id, false);

        (await versionService.GetCurrentIdAsync(game.Id)).ShouldBe(v1.Id);
        (await GetGameAsync(game.Id)).OptionSchema.ShouldBe("S1");
    }

    [Fact]
    public async Task GameWithoutPublishedVersionsOffersItsNewest()
    {
        var versionService = GetService<GameVersionService>();
        // A hidden game's first version is a draft
        var game = await GetService<GameService>().AddAsync(new Game { Title = Unique("Hidden"), Published = false });

        var v1 = await versionService.CreateAsync(game.Id, "1.0");

        (await versionService.GetWithConfigAsync(v1.Id))!.Published.ShouldBeFalse();
        (await versionService.GetCurrentIdAsync(game.Id)).ShouldBe(v1.Id);
    }
}
