using System.Linq.Expressions;
using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the server's games without exposing the database model.</summary>
internal sealed class GameProvider(
    GameService games,
    GameVersionService versions,
    GameCustomFieldService customFields,
    CategoryService categories,
    CollectionService collections,
    CompanyService companies,
    EngineService engines,
    GenreService genres,
    PlatformService platforms,
    TagService tags,
    RedistributableService redistributables,
    IDbContextFactory<DatabaseContext> contextFactory,
    IFusionCache cache) : IGameProvider
{
    public async Task<IReadOnlyList<GameInfo>> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Ordered(await games.GetAsync());
    }

    public async Task<GameInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var game = await games.GetAsync(id);

        return game == null ? null : Info(game);
    }

    public async Task<IReadOnlyList<GameInfo>> GetAddonsAsync(Guid baseGameId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Ordered(await games.GetAsync(game => game.BaseGameId == baseGameId));
    }

    public async Task<GameInfo?> FindByExternalIdAsync(string provider, string externalId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var lowered = provider.Trim().ToLower();
        var gameId = await context.Set<GameExternalId>()
            .Where(id => id.Provider.ToLower() == lowered && id.ExternalId == externalId && id.GameId != null)
            .Select(id => id.GameId)
            .FirstOrDefaultAsync(cancellationToken);

        return gameId == null ? null : await GetAsync(gameId.Value, cancellationToken);
    }

    public async Task<GameInfo> AddAsync(GameInfo game, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await CheckReferencesAsync(game);

        var entity = new Game { Id = game.Id };

        Apply(game, entity);

        var added = await games.AddAsync(entity);

        return Info(RecordGuard.Found(await games.GetAsync(added.Id), "game", added.Id));
    }

    public async Task<GameInfo> UpdateAsync(GameInfo game, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // CreatedBy is loaded so the update keeps it; see BaseDatabaseService.UpdateAsync
        var entity = RecordGuard.Found(await games.Include(g => g.CreatedBy).GetAsync(game.Id), "game", game.Id);

        await CheckReferencesAsync(game);

        Apply(game, entity);

        await games.UpdateAsync(entity);

        return Info(RecordGuard.Found(await games.GetAsync(game.Id), "game", game.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (await games.ExistsAsync(id))
            await games.DeleteAsync(new Game { Id = id });
    }

    public async Task<IReadOnlyList<Guid>> GetLinksAsync(Guid gameId, GameLinkKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Expression<Func<Game, IEnumerable<Guid>>> ids = kind switch
        {
            GameLinkKind.Categories => game => game.Categories!.Select(record => record.Id),
            GameLinkKind.Collections => game => game.Collections.Select(record => record.Id),
            GameLinkKind.Developers => game => game.Developers!.Select(record => record.Id),
            GameLinkKind.Genres => game => game.Genres!.Select(record => record.Id),
            GameLinkKind.Platforms => game => game.Platforms!.Select(record => record.Id),
            GameLinkKind.Publishers => game => game.Publishers!.Select(record => record.Id),
            GameLinkKind.Tags => game => game.Tags!.Select(record => record.Id),
            GameLinkKind.Redistributables => game => game.Redistributables!.Select(record => record.Id),
            GameLinkKind.Tools => game => game.Tools!.Select(record => record.Id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

        var linked = await games.GetAsync(gameId, query => query.Select(ids).Select(list => list.ToList()));

        return RecordGuard.Found(linked, "game", gameId);
    }

    public async Task SetLinksAsync(Guid gameId, GameLinkKind kind, IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var wanted = ids.Distinct().ToList();

        switch (kind)
        {
            case GameLinkKind.Categories:
                await SetAsync(gameId, wanted, categories, "category", g => g.Categories!, (g, records) => g.Categories = records);
                break;
            case GameLinkKind.Collections:
                await SetAsync(gameId, wanted, collections, "collection", g => g.Collections, (g, records) => g.Collections = records);
                break;
            case GameLinkKind.Developers:
                await SetAsync(gameId, wanted, companies, "company", g => g.Developers!, (g, records) => g.Developers = records);
                break;
            case GameLinkKind.Genres:
                await SetAsync(gameId, wanted, genres, "genre", g => g.Genres!, (g, records) => g.Genres = records);
                break;
            case GameLinkKind.Platforms:
                await SetAsync(gameId, wanted, platforms, "platform", g => g.Platforms!, (g, records) => g.Platforms = records);
                break;
            case GameLinkKind.Publishers:
                await SetAsync(gameId, wanted, companies, "company", g => g.Publishers!, (g, records) => g.Publishers = records);
                break;
            case GameLinkKind.Tags:
                await SetAsync(gameId, wanted, tags, "tag", g => g.Tags!, (g, records) => g.Tags = records);
                break;
            case GameLinkKind.Redistributables:
                await SetRedistributablesAsync(gameId, wanted);
                break;
            case GameLinkKind.Tools:
                await SetToolsAsync(gameId, wanted, cancellationToken);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    public async Task<string?> GetRedistributableOptionsAsync(Guid gameId, Guid redistributableId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await games.GetRedistributableOptionsAsync(gameId, redistributableId);
    }

    public async Task SetRedistributableOptionsAsync(Guid gameId, Guid redistributableId, string? optionsJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);

        await games.SetRedistributableOptionsAsync(gameId, redistributableId, optionsJson!);
    }

    public async Task<IReadOnlyList<GameExternalIdInfo>> GetExternalIdsAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await RequireGameAsync(context, gameId, cancellationToken);

        return await context.Set<GameExternalId>()
            .Where(id => id.GameId == gameId)
            .OrderBy(id => id.Provider)
            .Select(id => new GameExternalIdInfo { Id = id.Id, GameId = gameId, Provider = id.Provider, ExternalId = id.ExternalId })
            .ToListAsync(cancellationToken);
    }

    public async Task<GameExternalIdInfo> SetExternalIdAsync(Guid gameId, string provider, string externalId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException("A provider is required.", nameof(provider));

        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("An external id is required.", nameof(externalId));

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        await RequireGameAsync(context, gameId, cancellationToken);

        var lowered = provider.Trim().ToLower();
        var existing = await context.Set<GameExternalId>()
            .Where(id => id.GameId == gameId && id.Provider.ToLower() == lowered)
            .ToListAsync(cancellationToken);

        var record = existing.FirstOrDefault();

        if (record == null)
        {
            record = new GameExternalId { Id = Guid.NewGuid(), GameId = gameId, CreatedOn = DateTime.UtcNow };
            context.Add(record);
        }

        // A game has one id per service; any duplicates from before go
        context.RemoveRange(existing.Skip(1));

        record.Provider = provider.Trim();
        record.ExternalId = externalId.Trim();
        record.UpdatedOn = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.ExpireGameCacheAsync(gameId);

        return new GameExternalIdInfo { Id = record.Id, GameId = gameId, Provider = record.Provider, ExternalId = record.ExternalId };
    }

    public async Task RemoveExternalIdAsync(Guid gameId, string provider, CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var lowered = provider.Trim().ToLower();
        var existing = await context.Set<GameExternalId>()
            .Where(id => id.GameId == gameId && id.Provider.ToLower() == lowered)
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
            return;

        context.RemoveRange(existing);

        await context.SaveChangesAsync(cancellationToken);
        await cache.ExpireGameCacheAsync(gameId);
    }

    public async Task<IReadOnlyList<GameCustomFieldInfo>> GetCustomFieldsAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);

        return (await customFields.GetAsync(field => field.GameId == gameId))
            .OrderBy(field => field.Name, StringComparer.OrdinalIgnoreCase)
            .Select(CustomFieldInfo)
            .ToList();
    }

    public async Task<GameCustomFieldInfo> SetCustomFieldAsync(Guid gameId, string name, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A name is required.", nameof(name));

        if (!await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);

        return CustomFieldInfo(await games.SetCustomFieldAsync(gameId, name, value ?? ""));
    }

    public async Task RemoveCustomFieldAsync(Guid gameId, string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var field in await customFields.GetAsync(field => field.GameId == gameId && field.Name == name))
            await customFields.DeleteAsync(field);
    }

    /// <summary>
    /// Replaces one of a game's many-to-many links. The game is loaded with just that navigation, so
    /// the update leaves the others alone and an empty list clears it.
    /// </summary>
    async Task SetAsync<T>(
        Guid gameId,
        List<Guid> ids,
        BaseDatabaseService<T> service,
        string what,
        Expression<Func<Game, object>> navigation,
        Action<Game, List<T>> set) where T : class, IBaseModel
    {
        var records = ids.Count == 0 ? [] : (await service.GetAsync(record => ids.Contains(record.Id))).ToList();

        RecordGuard.AllFound(ids, records.Select(record => record.Id), what);

        var game = RecordGuard.Found(await games.Include(navigation, g => g.CreatedBy).GetAsync(gameId), "game", gameId);

        set(game, records);

        await games.UpdateAsync(game);
    }

    /// <summary>
    /// Redistributables belong to the latest version, and setting them there updates the game's
    /// own list, which mirrors it. Options are kept for the ones that stay.
    /// </summary>
    async Task SetRedistributablesAsync(Guid gameId, List<Guid> ids)
    {
        var found = ids.Count == 0 ? [] : (await redistributables.GetAsync(r => ids.Contains(r.Id), query => query.Select(r => r.Id))).ToList();

        RecordGuard.AllFound(ids, found, "redistributable");

        if (!await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);

        await versions.SetRedistributablesAsync(await versions.GetOrCreateLatestIdAsync(gameId), ids);
    }

    /// <summary>GameService does not sync a game's tools, so they are set here directly.</summary>
    async Task SetToolsAsync(Guid gameId, List<Guid> ids, CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

        var game = await context.Games!.Include(g => g.Tools).FirstOrDefaultAsync(g => g.Id == gameId, cancellationToken)
                   ?? throw RecordGuard.NotFound("game", gameId);

        var tools = await context.Set<Tool>().Where(tool => ids.Contains(tool.Id)).ToListAsync(cancellationToken);

        RecordGuard.AllFound(ids, tools.Select(tool => tool.Id), "tool");

        game.Tools ??= new List<Tool>();
        game.Tools.Clear();

        foreach (var tool in tools)
            game.Tools.Add(tool);

        game.UpdatedOn = DateTime.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        await cache.ExpireGameCacheAsync(gameId);
    }

    async Task CheckReferencesAsync(GameInfo game)
    {
        if (string.IsNullOrWhiteSpace(game.Title))
            throw new ArgumentException("A game needs a title.", nameof(game));

        if (game.BaseGameId is { } baseGameId && baseGameId != Guid.Empty && !await games.ExistsAsync(baseGameId))
            throw RecordGuard.NotFound("base game", baseGameId);

        if (game.EngineId is { } engineId && engineId != Guid.Empty && !await engines.ExistsAsync(engineId))
            throw RecordGuard.NotFound("engine", engineId);
    }

    static async Task RequireGameAsync(DatabaseContext context, Guid gameId, CancellationToken cancellationToken)
    {
        if (!await context.Games!.AnyAsync(game => game.Id == gameId, cancellationToken))
            throw RecordGuard.NotFound("game", gameId);
    }

    static void Apply(GameInfo info, Game entity)
    {
        entity.Title = info.Title.Trim();
        entity.SortTitle = info.SortTitle;
        entity.DirectoryName = info.DirectoryName;
        entity.Description = info.Description;
        entity.Notes = info.Notes;
        entity.ReleasedOn = info.ReleasedOn;
        entity.Type = info.Type;
        entity.BaseGameId = info.BaseGameId == Guid.Empty ? null : info.BaseGameId;
        entity.InstallTo = info.InstallTo;
        entity.ShowInLibrary = info.ShowInLibrary;
        entity.Singleplayer = info.Singleplayer;
        entity.Published = info.Published;
        entity.KeyAllocationMethod = info.KeyAllocationMethod;
        entity.ValidKeyRegex = info.ValidKeyRegex;
        entity.EngineId = info.EngineId == Guid.Empty ? null : info.EngineId;
    }

    static IReadOnlyList<GameInfo> Ordered(IEnumerable<Game> entities) =>
        entities.Select(Info).OrderBy(game => game.SortTitle ?? game.Title, StringComparer.OrdinalIgnoreCase).ToList();

    internal static GameInfo Info(Game game) => new()
    {
        Id = game.Id,
        Title = game.Title,
        SortTitle = game.SortTitle,
        DirectoryName = game.DirectoryName,
        Description = game.Description,
        Notes = game.Notes,
        ReleasedOn = game.ReleasedOn,
        Type = game.Type,
        BaseGameId = game.BaseGameId,
        InstallTo = game.InstallTo,
        ShowInLibrary = game.ShowInLibrary,
        Singleplayer = game.Singleplayer,
        Published = game.Published,
        KeyAllocationMethod = game.KeyAllocationMethod,
        ValidKeyRegex = game.ValidKeyRegex,
        EngineId = game.EngineId,
        CreatedOn = game.CreatedOn,
        UpdatedOn = game.UpdatedOn,
    };

    static GameCustomFieldInfo CustomFieldInfo(GameCustomField field) => new()
    {
        Id = field.Id,
        GameId = field.GameId ?? Guid.Empty,
        Name = field.Name,
        Value = field.Value,
    };
}
