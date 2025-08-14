using System;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderClient(this IServiceCollection services, Action<LANCommanderClientOptions> configureOptions)
    {
        services.AddSingleton<BeaconService>();
        services.AddSingleton<DepotService>();
        services.AddSingleton<DiscoveryBeacon>();
        services.AddSingleton<DiscoveryProbe>();
        services.AddSingleton<GameService>();
        services.AddSingleton<IssueService>();
        services.AddSingleton<LauncherService>();
        services.AddSingleton<LibraryService>();
        services.AddSingleton<LobbyService>();
        services.AddSingleton<MediaService>();
        services.AddSingleton<PlaySessionService>();
        services.AddSingleton<ProfileService>();
        services.AddSingleton<RedistributableService>();
        services.AddSingleton<SaveService>();
        services.AddSingleton<ScriptService>();
        services.AddSingleton<ServerService>();
        services.AddSingleton<TagService>();

        services.Configure(configureOptions);

        services.AddScoped<Client>();

        return services;
    }
}

public class LANCommanderClientOptions
{
    public string BaseUrl { get; set; }
    public string InstallDirectory { get; set; }
    public bool EnableScriptDebugging { get; set; }
    public event ScriptService.ExternalScriptRunnerHandler ExternalScriptRunner;
}