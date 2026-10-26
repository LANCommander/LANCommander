using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>A CD key for a game, handed out to players as the game's allocation method decides.</summary>
public sealed record KeyInfo
{
    public Guid Id { get; init; }
    public string Value { get; init; } = "";

    /// <summary>How the key was claimed, or null while it is free. Set by the server; ignored when writing.</summary>
    public KeyAllocationMethod? AllocationMethod { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public Guid? ClaimedByUserId { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public string? ClaimedByMacAddress { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public string? ClaimedByIpv4Address { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public string? ClaimedByComputerName { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime? ClaimedOn { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public RecordOwner Owner { get; init; }
}

/// <summary>
/// Manages the keys of games. Provided by the server; inject it into a scoped plugin service or
/// page. <see cref="IOwnedRecordProvider{TInfo}.UpdateAsync"/> changes a key's value only.
/// </summary>
public interface IKeyProvider : IOwnedRecordProvider<KeyInfo>
{
    /// <summary>Adds several keys to a game, skipping blank values and ones it already has.</summary>
    /// <returns>The keys added.</returns>
    /// <exception cref="KeyNotFoundException">There is no such game.</exception>
    Task<IReadOnlyList<KeyInfo>> AddRangeAsync(Guid gameId, IEnumerable<string> values, CancellationToken cancellationToken = default);

    /// <summary>Frees a claimed key so it can be handed out again.</summary>
    /// <exception cref="KeyNotFoundException">There is no such key.</exception>
    Task<KeyInfo> ReleaseAsync(Guid id, CancellationToken cancellationToken = default);
}
