using LANCommander.Server.Services.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace LANCommander.Server.Startup;

/// <summary>
/// Wires up the shared infrastructure required to run multiple server instances against one
/// database, cache, and filesystem. Everything here is gated behind
/// <see cref="Settings.Models.ScalingSettings.Enabled"/>; when scaling is disabled the server keeps
/// its single-instance, in-process behavior and no Redis connection is made.
/// </summary>
public static class Scaling
{
    private const string DataProtectionKeyName = "LANCommander-DataProtection-Keys";

    /// <summary>
    /// Reads scaling settings and, when enabled, registers a shared Redis connection plus the
    /// Redis-backed data-protection key ring and distributed cache / FusionCache backplane.
    /// </summary>
    public static WebApplicationBuilder AddScaling(this WebApplicationBuilder builder)
    {
        var scaling = GetScalingSettings(builder);

        if (!scaling.Enabled)
            return builder;

        if (string.IsNullOrWhiteSpace(scaling.RedisConnectionString))
            throw new InvalidOperationException(
                "Server.Scaling.Enabled is true but Server.Scaling.RedisConnectionString is not set. " +
                "A Redis connection is required for horizontal scaling.");

        // A single shared multiplexer reused by data protection, the distributed cache, the SignalR
        // backplane, Hangfire, and the coordinator lease.
        var multiplexer = ConnectionMultiplexer.Connect(scaling.RedisConnectionString);
        builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);

        // Shared data-protection keys so Identity auth cookies and Blazor circuits issued by one
        // instance can be read by any other instance.
        builder.Services
            .AddDataProtection()
            .PersistKeysToStackExchangeRedis(multiplexer, DataProtectionKeyName)
            .SetApplicationName("LANCommander");

        // Redis-backed distributed cache + FusionCache backplane. FusionCache picks these up via
        // TryWithAutoSetup() where it is registered in the Services project.
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = scaling.RedisConnectionString;
        });

        builder.Services.AddFusionCacheSystemTextJsonSerializer();
        builder.Services.AddFusionCacheStackExchangeRedisBackplane(options =>
        {
            options.Configuration = scaling.RedisConnectionString;
        });

        // Replace the always-leader single-instance election with a Redis-lease election so
        // coordinator-only work runs on exactly one node. The same instance is exposed as both the
        // election service and a hosted service that renews the lease on a timer.
        builder.Services.RemoveAll<ICoordinatorElection>();
        builder.Services.AddSingleton<RedisCoordinatorElection>();
        builder.Services.AddSingleton<ICoordinatorElection>(sp => sp.GetRequiredService<RedisCoordinatorElection>());
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RedisCoordinatorElection>());

        return builder;
    }

    /// <summary>
    /// True when horizontal-scaling mode is enabled in settings. Startup extensions use this to
    /// conditionally add Redis backplanes / storage.
    /// </summary>
    public static bool IsScalingEnabled(this WebApplicationBuilder builder) =>
        GetScalingSettings(builder).Enabled;

    /// <summary>
    /// The configured Redis connection string, or empty when scaling is disabled.
    /// </summary>
    public static string GetRedisConnectionString(this WebApplicationBuilder builder) =>
        GetScalingSettings(builder).RedisConnectionString;

    private static Settings.Models.ScalingSettings GetScalingSettings(WebApplicationBuilder builder)
    {
        var settings = new Settings.Settings();
        builder.Configuration.Bind(settings);
        return settings.Server.Scaling;
    }
}
