using LANCommander.Server.Data.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Plugins.Records;

/// <summary>Gives plugins the multiplayer modes of games without exposing the database model.</summary>
internal sealed class MultiplayerModeProvider(MultiplayerModeService modes, RecordOwners owners) : IMultiplayerModeProvider
{
    public async Task<IReadOnlyList<MultiplayerModeInfo>> GetAsync(RecordOwner owner, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Multiplayer modes", RecordOwnerKind.Game);

        await owners.RequireAsync(owner);

        var id = owner.Id;

        return (await modes.GetAsync(m => m.GameId == id))
            .OrderBy(m => m.Type)
            .Select(Info)
            .ToList();
    }

    public async Task<MultiplayerModeInfo?> GetRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mode = await modes.GetAsync(id);

        return mode == null ? null : Info(mode);
    }

    public async Task<MultiplayerModeInfo> AddAsync(RecordOwner owner, MultiplayerModeInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        RecordGuard.OwnerIs(owner, "Multiplayer modes", RecordOwnerKind.Game);

        await owners.RequireAsync(owner);

        var entity = new MultiplayerMode { Id = record.Id, GameId = owner.Id };

        Apply(record, entity);

        var added = await modes.AddAsync(entity);

        return Info(RecordGuard.Found(await modes.GetAsync(added.Id), "multiplayer mode", added.Id));
    }

    public async Task<MultiplayerModeInfo> UpdateAsync(MultiplayerModeInfo record, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entity = RecordGuard.Found(await modes.Include(m => m.CreatedBy!).GetAsync(record.Id), "multiplayer mode", record.Id);

        Apply(record, entity);

        await modes.UpdateAsync(entity);

        return Info(RecordGuard.Found(await modes.GetAsync(record.Id), "multiplayer mode", record.Id));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var mode = await modes.GetAsync(id);

        if (mode != null)
            await modes.DeleteAsync(mode);
    }

    static void Apply(MultiplayerModeInfo record, MultiplayerMode entity)
    {
        if (record.MinPlayers < 0 || record.MaxPlayers < 0 || record.Spectators < 0)
            throw new ArgumentException("Player counts cannot be negative.", nameof(record));

        entity.Type = record.Type;
        entity.NetworkProtocol = record.NetworkProtocol;
        entity.Description = record.Description;
        entity.MinPlayers = record.MinPlayers;
        entity.MaxPlayers = record.MaxPlayers;
        entity.Spectators = record.Spectators;
    }

    static MultiplayerModeInfo Info(MultiplayerMode mode) => new()
    {
        Id = mode.Id,
        Type = mode.Type,
        NetworkProtocol = mode.NetworkProtocol,
        Description = mode.Description,
        MinPlayers = mode.MinPlayers,
        MaxPlayers = mode.MaxPlayers,
        Spectators = mode.Spectators,
        Owner = RecordOwner.Game(mode.GameId ?? Guid.Empty),
    };
}
