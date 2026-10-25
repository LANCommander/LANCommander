using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the versions of the server's games without exposing the database model.</summary>
internal sealed class GameVersionProvider(
    GameService games,
    GameVersionService versions,
    ArchiveService archives) : IGameVersionProvider
{
    public async Task<IReadOnlyList<GameVersionInfo>> GetAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (await versions.Include(v => v.Archive!).GetAsync(v => v.GameId == gameId))
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.CreatedOn)
            .Select(Info)
            .ToList();
    }

    public async Task<GameVersionInfo?> GetLatestAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = await versions.GetLatestAsync(gameId);

        return version == null ? null : Info(version);
    }

    public async Task<GameVersionInfo?> GetVersionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = await versions.Include(v => v.Archive!).GetAsync(id);

        return version == null ? null : Info(version);
    }

    public async Task<GameVersionInfo> CreateAsync(Guid gameId, string version, string? changelog = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("A version number is required.", nameof(version));

        if (!await games.ExistsAsync(gameId))
            throw RecordGuard.NotFound("game", gameId);

        var created = await versions.CreateAsync(gameId, version.Trim(), changelog);

        return RecordGuard.Found(await GetVersionAsync(created.Id, cancellationToken), "game version", created.Id);
    }

    public async Task<GameVersionInfo> UpdateAsync(GameVersionInfo version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(version.Version))
            throw new ArgumentException("A version number is required.", nameof(version));

        var entity = RecordGuard.Found(await versions.Include(v => v.CreatedBy!).GetAsync(version.Id), "game version", version.Id);
        var wasPublished = entity.Published;

        entity.Version = version.Version.Trim();
        entity.Changelog = version.Changelog;
        entity.SortOrder = version.SortOrder;
        entity.OptionSchema = version.OptionSchema;

        await versions.UpdateAsync(entity);

        // Publishing has rules of its own (the game's flag follows its versions)
        if (version.Published != wasPublished)
            await versions.SetPublishedAsync(version.Id, version.Published);

        return RecordGuard.Found(await GetVersionAsync(version.Id, cancellationToken), "game version", version.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var version = await versions.Include(v => v.Archive!).GetAsync(id);

        if (version == null)
            return;

        if (version.Archive != null)
            await archives.DeleteAsync(version.Archive);

        // The version's own actions, scripts and save paths are deleted with it on the client side,
        // so they have to be loaded
        var loaded = await versions
            .Include(v => v.Scripts!, v => v.Actions!, v => v.SavePaths!, v => v.Redistributables!)
            .GetAsync(id);

        if (loaded == null)
            return;

        await versions.DeleteAsync(loaded);

        // The game's redistributables mirror its current version, which may have been this one.
        // Setting the new current version's own list again mirrors it.
        if (await versions.GetCurrentIdAsync(version.GameId) is { } currentId)
            await versions.SetRedistributablesAsync(
                currentId,
                (await versions.GetRedistributablesAsync(currentId)).Select(r => r.RedistributableId).ToList());
    }

    static GameVersionInfo Info(GameVersion version) => new()
    {
        Id = version.Id,
        GameId = version.GameId,
        Version = version.Version,
        Changelog = version.Changelog,
        SortOrder = version.SortOrder,
        Published = version.Published,
        OptionSchema = version.OptionSchema,
        ArchiveId = version.Archive?.Id,
        CreatedOn = version.CreatedOn,
    };
}
