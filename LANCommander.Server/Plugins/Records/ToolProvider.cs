using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the server's tools without exposing the database model.</summary>
internal sealed class ToolProvider(
    ToolService tools,
    GameService games,
    ArchiveService archives,
    IFusionCache cache) : IToolProvider
{
    public async Task<IReadOnlyList<ToolInfo>> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (await tools.GetAsync())
            .Select(Info)
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ToolInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var tool = await tools.GetAsync(id);

        return tool == null ? null : Info(tool);
    }

    public async Task<ToolInfo?> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lowered = name.Trim().ToLower();
        var tool = await tools.FirstOrDefaultAsync(t => t.Name.ToLower() == lowered);

        return tool == null ? null : Info(tool);
    }

    public async Task<ToolInfo> AddAsync(ToolInfo tool, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = new Tool { Id = tool.Id };

        Apply(tool, entity);

        var added = await tools.AddAsync(entity);

        return Info(RecordGuard.Found(await tools.GetAsync(added.Id), "tool", added.Id));
    }

    public async Task<ToolInfo> UpdateAsync(ToolInfo tool, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await tools.Include(t => t.CreatedBy!).GetAsync(tool.Id), "tool", tool.Id);

        Apply(tool, entity);

        await tools.UpdateAsync(entity);

        return Info(RecordGuard.Found(await tools.GetAsync(tool.Id), "tool", tool.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await tools.Include(t => t.Archives!).GetAsync(id);

        if (entity == null)
            return;

        // The service deletes the record alone; the archive files would be left behind
        foreach (var archive in entity.Archives?.ToList() ?? [])
            await archives.DeleteAsync(archive);

        await tools.DeleteAsync(new Tool { Id = id });
    }

    public async Task<IReadOnlyList<Guid>> GetGamesAsync(Guid toolId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ids = await tools.GetAsync(toolId, query => query.Select(t => t.Games!.Select(g => g.Id).ToList()));

        return RecordGuard.Found(ids, "tool", toolId);
    }

    public async Task SetGamesAsync(Guid toolId, IEnumerable<Guid> gameIds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var wanted = gameIds.Distinct().ToList();
        var linked = wanted.Count == 0 ? [] : (await games.GetAsync(g => wanted.Contains(g.Id))).ToList();

        RecordGuard.AllFound(wanted, linked.Select(g => g.Id), "game");

        // Loaded with just its games, so the update leaves its other links alone and an empty list clears them
        var tool = RecordGuard.Found(await tools.Include(t => t.Games!, t => t.CreatedBy!).GetAsync(toolId), "tool", toolId);
        var previous = tool.Games?.Select(g => g.Id).ToList() ?? [];

        tool.Games = linked;

        await tools.UpdateAsync(tool);

        // ToolService expires the cache of the games it links; the ones it unlinked change too
        foreach (var gameId in previous.Except(wanted))
            await cache.ExpireGameCacheAsync(gameId);
    }

    static void Apply(ToolInfo info, Tool entity)
    {
        if (string.IsNullOrWhiteSpace(info.Name))
            throw new ArgumentException("A tool needs a name.", nameof(info));

        entity.Name = info.Name.Trim();
        entity.Description = info.Description;
        entity.Notes = info.Notes;
        entity.AlwaysInstall = info.AlwaysInstall;
    }

    static ToolInfo Info(Tool entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Notes = entity.Notes,
        AlwaysInstall = entity.AlwaysInstall,
        CreatedOn = entity.CreatedOn,
        UpdatedOn = entity.UpdatedOn,
    };
}
