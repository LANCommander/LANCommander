namespace LANCommander.Server.Plugins;

/// <summary>
/// Limits how often plugins can get a password wrong for one user name, so a plugin that exposes
/// <see cref="IUserDirectory.CheckPasswordAsync"/> cannot be used to guess passwords at speed.
/// </summary>
/// <remarks>
/// Every attempt is counted as a failure when it starts and forgiven only if it succeeds, so a burst
/// of concurrent attempts cannot all slip under the limit before any of them fails. Counts are kept
/// per user name, whether or not the user exists, and only in memory.
/// </remarks>
internal sealed class PasswordCheckRateLimiter(TimeProvider timeProvider)
{
    /// <summary>How many failures a user name may have within <see cref="Window"/>.</summary>
    public const int MaxFailures = 5;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    const int SweepInterval = 256;

    readonly Dictionary<string, Queue<DateTimeOffset>> _failures = new(StringComparer.OrdinalIgnoreCase);
    readonly Lock _lock = new();
    int _attemptsSinceSweep;

    /// <summary>
    /// Starts an attempt for a user name, counting it as a failure until <see cref="Succeeded"/> is
    /// called. Returns null when the attempt may go ahead, or how long until it may be retried.
    /// </summary>
    public TimeSpan? TryStart(string userName)
    {
        var now = timeProvider.GetUtcNow();

        lock (_lock)
        {
            if (++_attemptsSinceSweep >= SweepInterval)
                Sweep(now);

            if (!_failures.TryGetValue(userName, out var failures))
                _failures[userName] = failures = new Queue<DateTimeOffset>();

            Expire(failures, now);

            if (failures.Count >= MaxFailures)
                return failures.Peek() + Window - now;

            failures.Enqueue(now);

            return null;
        }
    }

    /// <summary>Forgives the user name's failures after a successful check.</summary>
    public void Succeeded(string userName)
    {
        lock (_lock)
            _failures.Remove(userName);
    }

    static void Expire(Queue<DateTimeOffset> failures, DateTimeOffset now)
    {
        while (failures.Count > 0 && failures.Peek() + Window <= now)
            failures.Dequeue();
    }

    // Drops user names whose failures have all expired, so names that are tried once and never
    // again do not accumulate
    void Sweep(DateTimeOffset now)
    {
        _attemptsSinceSweep = 0;

        foreach (var (userName, failures) in _failures.ToList())
        {
            Expire(failures, now);

            if (failures.Count == 0)
                _failures.Remove(userName);
        }
    }
}
