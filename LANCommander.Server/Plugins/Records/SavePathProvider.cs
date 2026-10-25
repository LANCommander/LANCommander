using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the save paths of games without exposing the database model.</summary>
internal sealed class SavePathProvider(SavePathService savePaths, RecordOwners owners) : ISavePathProvider
{
    public async Task<IReadOnlyList<SavePathInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Save paths", RecordOwnerKind.Game);

        await owners.RequireAsync(owner);

        var id = owner.Id;
        var versionId = await owners.VersionOfAsync(owner, create: false);

        // Save paths from before versions belong to the game alone, and apply to every version
        return (await savePaths.GetAsync(p => p.GameId == id && (p.GameVersionId == null || p.GameVersionId == versionId)))
            .OrderBy(p => p.Type)
            .ThenBy(p => p.Path, StringComparer.OrdinalIgnoreCase)
            .Select(Info)
            .ToList();
    }

    public async Task<SavePathInfo?> GetRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var savePath = await savePaths.GetAsync(id);

        return savePath == null ? null : Info(savePath);
    }

    public async Task<SavePathInfo> AddAsync(RecordOwner owner, SavePathInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Save paths", RecordOwnerKind.Game);

        await owners.RequireAsync(owner);

        var entity = new SavePath
        {
            Id = record.Id,
            GameId = owner.Id,
            GameVersionId = await owners.VersionOfAsync(owner, create: true),
        };

        Apply(record, entity);

        var added = await savePaths.AddAsync(entity);

        return Info(RecordGuard.Found(await savePaths.GetAsync(added.Id), "save path", added.Id));
    }

    public async Task<SavePathInfo> UpdateAsync(SavePathInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await savePaths.Include(p => p.CreatedBy!).GetAsync(record.Id), "save path", record.Id);

        Apply(record, entity);

        await savePaths.UpdateAsync(entity);

        return Info(RecordGuard.Found(await savePaths.GetAsync(record.Id), "save path", record.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var savePath = await savePaths.GetAsync(id);

        if (savePath != null)
            await savePaths.DeleteAsync(savePath);
    }

    static void Apply(SavePathInfo record, SavePath entity)
    {
        if (string.IsNullOrWhiteSpace(record.Path))
            throw new ArgumentException("A save path needs a path.", nameof(record));

        entity.Type = record.Type;
        entity.Path = record.Path;
        entity.WorkingDirectory = record.WorkingDirectory;
        entity.IsRegex = record.IsRegex;
        entity.Platforms = record.Platforms;
    }

    static SavePathInfo Info(SavePath savePath) => new()
    {
        Id = savePath.Id,
        Type = savePath.Type,
        Path = savePath.Path,
        WorkingDirectory = savePath.WorkingDirectory,
        IsRegex = savePath.IsRegex,
        Platforms = savePath.Platforms,
        Owner = RecordOwner.Game(savePath.GameId ?? Guid.Empty, savePath.GameVersionId),
    };
}
