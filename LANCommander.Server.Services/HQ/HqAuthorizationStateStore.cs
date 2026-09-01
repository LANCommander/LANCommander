using System.Collections.Concurrent;

namespace LANCommander.Server.Services.HQ;

/// <summary>
/// Tracks which administrators have actually started an HQ login, so the callback endpoint only
/// accepts a token for a flow that was initiated here moments ago.
///
/// The callback writes the server's HQ credential from a query parameter. Requiring the
/// authenticated caller to have clicked Connect first closes the login-CSRF hole where an admin's
/// browser is walked through an HQ login of someone else's choosing, silently re-pointing the
/// server at an attacker's account.
///
/// Keyed on the administrator's identity rather than a nonce echoed through HQ, because the token
/// relay owns the return URL and we cannot rely on it preserving query parameters we add.
/// </summary>
public sealed class HqAuthorizationStateStore
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<string, DateTimeOffset> _pending = new(StringComparer.Ordinal);

    /// <summary>Records that <paramref name="userName"/> has begun an HQ login.</summary>
    public void Issue(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return;

        Prune();

        _pending[userName] = DateTimeOffset.UtcNow + Lifetime;
    }

    /// <summary>
    /// Consumes a pending authorization. Single-use, so a replayed callback fails even inside the
    /// lifetime window.
    /// </summary>
    public bool TryConsume(string? userName)
    {
        Prune();

        if (string.IsNullOrWhiteSpace(userName))
            return false;

        return _pending.TryRemove(userName, out var expiresAt) && expiresAt > DateTimeOffset.UtcNow;
    }

    public void Revoke(string? userName)
    {
        if (!string.IsNullOrWhiteSpace(userName))
            _pending.TryRemove(userName, out _);
    }

    private void Prune()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var (userName, expiresAt) in _pending)
        {
            if (expiresAt <= now)
                _pending.TryRemove(userName, out _);
        }
    }
}
