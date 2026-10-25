using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the server's redistributables without exposing the database model.</summary>
internal sealed class RedistributableProvider(
    RedistributableService redistributables,
    ArchiveService archives) : IRedistributableProvider
{
    public async Task<IReadOnlyList<RedistributableInfo>> GetAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return (await redistributables.Include(r => r.Archives!).GetAsync())
            .Select(Info)
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<RedistributableInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var redistributable = await redistributables.Include(r => r.Archives!).GetAsync(id);

        return redistributable == null ? null : Info(redistributable);
    }

    public async Task<RedistributableInfo?> FindAsync(string name, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var lowered = name.Trim().ToLower();
        var redistributable = await redistributables.Include(r => r.Archives!).FirstOrDefaultAsync(r => r.Name.ToLower() == lowered);

        return redistributable == null ? null : Info(redistributable);
    }

    public async Task<RedistributableInfo> AddAsync(RedistributableInfo redistributable, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = new Redistributable { Id = redistributable.Id };

        Apply(redistributable, entity);

        var added = await redistributables.AddAsync(entity);

        return RecordGuard.Found(await GetAsync(added.Id, cancellationToken), "redistributable", added.Id);
    }

    public async Task<RedistributableInfo> UpdateAsync(RedistributableInfo redistributable, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(
            await redistributables.Include(r => r.CreatedBy!).GetAsync(redistributable.Id),
            "redistributable",
            redistributable.Id);

        Apply(redistributable, entity);

        await redistributables.UpdateAsync(entity);

        return RecordGuard.Found(await GetAsync(redistributable.Id, cancellationToken), "redistributable", redistributable.Id);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = await redistributables.Include(r => r.Archives!).GetAsync(id);

        if (entity == null)
            return;

        // The service deletes the record alone; the archive files would be left behind
        foreach (var archive in entity.Archives?.ToList() ?? [])
            await archives.DeleteAsync(archive);

        await redistributables.DeleteAsync(new Redistributable { Id = id });
    }

    static void Apply(RedistributableInfo info, Redistributable entity)
    {
        if (string.IsNullOrWhiteSpace(info.Name))
            throw new ArgumentException("A redistributable needs a name.", nameof(info));

        entity.Name = info.Name.Trim();
        entity.Description = info.Description;
        entity.Notes = info.Notes;
        entity.OptionSchema = info.OptionSchema;
    }

    static RedistributableInfo Info(Redistributable entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        Notes = entity.Notes,
        OptionSchema = entity.OptionSchema,
        Version = entity.Archives is { Count: > 0 } ? MapperFunctions.LatestArchiveVersion(entity.Archives) : null,
        CreatedOn = entity.CreatedOn,
        UpdatedOn = entity.UpdatedOn,
    };
}
