using LANCommander.Server.Services.Abstractions;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace LANCommander.Server.Startup;

/// <summary>
/// Redis-backed <see cref="ICoordinatorElection"/> used in horizontal-scaling mode. Exactly one
/// instance holds the coordinator lease at a time; the holder runs coordinator-only work (boot
/// autostart, autostop scheduling, the play-session sweep, the IPX relay, the discovery beacon, and
/// database migrations). The lease is a single Redis key with a short TTL that the holder renews on
/// a timer, so if the coordinator dies its lease expires and another instance takes over.
/// </summary>
public sealed class RedisCoordinatorElection : BackgroundService, ICoordinatorElection
{
    private const string LeaseKey = "LANCommander:Coordinator:Leader";

    // Renew well within the TTL so transient latency doesn't cost us the lease.
    private static readonly TimeSpan LeaseTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RenewInterval = TimeSpan.FromSeconds(10);

    // Only extend/release the lease if we still own it, so we never steal another node's lease.
    private const string RenewScript = @"
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('pexpire', KEYS[1], ARGV[2])
        else
            return 0
        end";

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ILogger<RedisCoordinatorElection> _logger;
    private readonly string _instanceId;

    private volatile bool _isLeader;

    public RedisCoordinatorElection(
        IConnectionMultiplexer multiplexer,
        SettingsProvider<Settings.Settings> settingsProvider,
        ILogger<RedisCoordinatorElection> logger)
    {
        _multiplexer = multiplexer;
        _logger = logger;

        // A stable-per-process identity so renewals recognize our own lease. Fall back to a GUID
        // when no instance name is configured.
        var instanceName = settingsProvider.CurrentValue.Server.Scaling.InstanceName;
        _instanceId = string.IsNullOrWhiteSpace(instanceName)
            ? Guid.NewGuid().ToString("N")
            : $"{instanceName}:{Guid.NewGuid():N}";
    }

    public bool IsLeader => _isLeader;

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _multiplexer.GetDatabase();

            // If we already hold the lease, renew it; otherwise try to grab it while it's free.
            var renewed = (long)await db.ScriptEvaluateAsync(
                RenewScript,
                new RedisKey[] { LeaseKey },
                new RedisValue[] { _instanceId, (long)LeaseTtl.TotalMilliseconds }) == 1;

            var acquired = renewed || await db.StringSetAsync(
                LeaseKey, _instanceId, LeaseTtl, When.NotExists);

            if (acquired && !_isLeader)
                _logger.LogInformation("Acquired coordinator lease as {InstanceId}", _instanceId);
            else if (!acquired && _isLeader)
                _logger.LogWarning("Lost coordinator lease {InstanceId}", _instanceId);

            _isLeader = acquired;
            return acquired;
        }
        catch (Exception ex)
        {
            // On Redis errors, relinquish leadership so coordinator-only work doesn't run split-brain.
            _logger.LogError(ex, "Coordinator lease check failed; relinquishing leadership");
            _isLeader = false;
            return false;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await TryAcquireAsync(stoppingToken);

            try
            {
                await Task.Delay(RenewInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        // Best-effort release on graceful shutdown so failover is immediate rather than waiting out
        // the TTL.
        if (_isLeader)
        {
            try
            {
                var db = _multiplexer.GetDatabase();
                await db.ScriptEvaluateAsync(
                    @"if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                    new RedisKey[] { LeaseKey },
                    new RedisValue[] { _instanceId });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release coordinator lease on shutdown");
            }
        }
    }
}
