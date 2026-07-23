namespace LANCommander.Server.Settings.Models;

/// <summary>
/// Configuration for running multiple LANCommander.Server instances against a shared database,
/// cache, and filesystem. When <see cref="Enabled"/> is false the server runs in single-instance
/// mode and none of the shared coordination infrastructure (Redis, leader election) is used.
/// </summary>
public class ScalingSettings
{
    /// <summary>
    /// Enables horizontal-scaling mode. Requires <see cref="RedisConnectionString"/> to be set and
    /// a non-SQLite database provider.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// StackExchange.Redis connection string used for the distributed cache, SignalR backplane,
    /// data-protection key ring, Hangfire storage, and leader-election lease.
    /// </summary>
    public string RedisConnectionString { get; set; } = String.Empty;

    /// <summary>
    /// Friendly name for this instance, surfaced in logs and the coordinator lease. Defaults to the
    /// machine name when left blank.
    /// </summary>
    public string InstanceName { get; set; } = String.Empty;
}
