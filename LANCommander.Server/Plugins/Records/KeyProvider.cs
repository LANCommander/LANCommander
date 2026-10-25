using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Extensions;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the keys of games without exposing the database model.</summary>
internal sealed class KeyProvider(KeyService keys, RecordOwners owners, IFusionCache cache) : IKeyProvider
{
    public async Task<IReadOnlyList<KeyInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Keys", RecordOwnerKind.Game);

        await owners.RequireAsync(owner);

        var id = owner.Id;

        return (await keys.GetAsync(k => k.GameId == id))
            .OrderBy(k => k.CreatedOn)
            .Select(Info)
            .ToList();
    }

    public async Task<KeyInfo?> GetRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = await keys.GetAsync(id);

        return key == null ? null : Info(key);
    }

    public async Task<KeyInfo> AddAsync(RecordOwner owner, KeyInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Keys", RecordOwnerKind.Game);

        await owners.RequireAsync(owner);

        var added = await keys.AddAsync(new Key { Id = record.Id, GameId = owner.Id, Value = Required(record.Value) });

        await cache.ExpireGameCacheAsync(owner.Id);

        return Info(RecordGuard.Found(await keys.GetAsync(added.Id), "key", added.Id));
    }

    public async Task<IReadOnlyList<KeyInfo>> AddRangeAsync(Guid gameId, IEnumerable<string> values, CancellationToken cancellationToken = default)
    {
        var owner = RecordOwner.Game(gameId);

        await owners.RequireAsync(owner);

        var existing = (await keys.GetAsync(k => k.GameId == gameId)).Select(k => k.Value).ToHashSet();
        var added = new List<KeyInfo>();

        foreach (var value in values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!existing.Add(value))
                continue;

            added.Add(Info(await keys.AddAsync(new Key { GameId = gameId, Value = value })));
        }

        await cache.ExpireGameCacheAsync(gameId);

        return added;
    }

    public async Task<KeyInfo> UpdateAsync(KeyInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await keys.Include(k => k.CreatedBy!).GetAsync(record.Id), "key", record.Id);

        entity.Value = Required(record.Value);

        await keys.UpdateAsync(entity);
        await cache.ExpireGameCacheAsync(entity.GameId);

        return Info(RecordGuard.Found(await keys.GetAsync(record.Id), "key", record.Id));
    }

    public async Task<KeyInfo> ReleaseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await keys.Include(k => k.CreatedBy!).GetAsync(id), "key", id);

        await keys.ReleaseAsync(entity);

        return Info(RecordGuard.Found(await keys.GetAsync(id), "key", id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = await keys.GetAsync(id);

        if (key == null)
            return;

        await keys.DeleteAsync(key);
        await cache.ExpireGameCacheAsync(key.GameId);
    }

    static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A key needs a value.", nameof(value)) : value.Trim();

    static KeyInfo Info(Key key) => new()
    {
        Id = key.Id,
        Value = key.Value,
        AllocationMethod = key.AllocationMethod,
        ClaimedByUserId = key.ClaimedByUserId,
        ClaimedByMacAddress = key.ClaimedByMacAddress,
        ClaimedByIpv4Address = key.ClaimedByIpv4Address,
        ClaimedByComputerName = key.ClaimedByComputerName,
        ClaimedOn = key.ClaimedOn,
        Owner = RecordOwner.Game(key.GameId),
    };
}
