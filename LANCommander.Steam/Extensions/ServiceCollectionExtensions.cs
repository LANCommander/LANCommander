using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LANCommander.Steam.Abstractions;
using LANCommander.Steam.Implementations;
using LANCommander.Steam.Options;
using LANCommander.Steam.Services;

namespace LANCommander.Steam.Extensions;

/// <summary>
/// Extension methods for registering SteamCMD services (optional, for DI-based hosts).
/// Steam types can also be constructed directly without DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add SteamCMD service with default in-memory profile store
    /// </summary>
    public static IServiceCollection AddSteamCmd(this IServiceCollection services, Action<SteamCmdOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SteamCmdOptions>(_ => { });
        }

        services.AddSingleton<ISteamCmdProfileStore, InMemorySteamCmdProfileStore>();
        services.AddScoped<ISteamCmdService>(sp => new SteamCmdService(
            sp.GetService<IOptions<SteamCmdOptions>>()?.Value,
            sp.GetService<ISteamCmdProfileStore>(),
            sp.GetService<ILogger<SteamCmdService>>()));

        return services;
    }

    /// <summary>
    /// Add SteamCMD service with custom profile store implementation
    /// </summary>
    public static IServiceCollection AddSteamCmd<TProfileStore>(
        this IServiceCollection services,
        Action<SteamCmdOptions>? configure = null)
        where TProfileStore : class, ISteamCmdProfileStore
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SteamCmdOptions>(_ => { });
        }

        services.AddSingleton<ISteamCmdProfileStore, TProfileStore>();
        services.AddScoped<ISteamCmdService>(sp => new SteamCmdService(
            sp.GetService<IOptions<SteamCmdOptions>>()?.Value,
            sp.GetService<ISteamCmdProfileStore>(),
            sp.GetService<ILogger<SteamCmdService>>()));

        return services;
    }

    /// <summary>
    /// Add SteamCMD service with existing profile store instance
    /// </summary>
    public static IServiceCollection AddSteamCmd(
        this IServiceCollection services,
        ISteamCmdProfileStore profileStore,
        Action<SteamCmdOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<SteamCmdOptions>(_ => { });
        }

        services.AddSingleton(profileStore);
        services.AddScoped<ISteamCmdService>(sp => new SteamCmdService(
            sp.GetService<IOptions<SteamCmdOptions>>()?.Value,
            sp.GetService<ISteamCmdProfileStore>(),
            sp.GetService<ILogger<SteamCmdService>>()));

        return services;
    }
}
