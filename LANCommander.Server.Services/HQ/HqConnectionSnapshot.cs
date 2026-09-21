namespace LANCommander.Server.Services.HQ;

/// <summary>
/// An immutable point-in-time view of the HQ connection. Immutability is the thread-safety
/// strategy: a single reference is swapped atomically, so readers never lock and never observe a
/// half-updated state.
/// </summary>
public sealed record HqConnectionSnapshot(
    HqConnectionStatus Status,
    string? Username,
    bool IsPremium,
    bool IsEditor,
    string? PreferredLocale,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? TokenExpiresAt,
    string? LastError)
{
    public static readonly HqConnectionSnapshot Disconnected =
        new(HqConnectionStatus.Disconnected, null, false, false, null, null, null, null);

    public static readonly HqConnectionSnapshot Unknown =
        new(HqConnectionStatus.Unknown, null, false, false, null, null, null, null);
}
