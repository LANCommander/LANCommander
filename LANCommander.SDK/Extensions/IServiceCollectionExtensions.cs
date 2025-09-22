using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Configuration;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommander(this IServiceCollection services)
    {
        services.AddSingleton<ILANCommanderConfiguration, LANCommanderConfiguration>();
        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddSingleton<INetworkInformationProvider, NetworkInformationProvider>();
        services.AddScoped<ApiRequestFactory>();
        services.AddScoped<ProcessExecutionContextFactory>();

        services.AddScoped<AuthenticationService>();
        services.AddSingleton<BeaconService>();
        services.AddScoped<ChatService>();
        services.AddSingleton<IConnectionService, ConnectionService>();
        services.AddScoped<DepotService>();
        services.AddScoped<GameService>();
        services.AddScoped<IssueService>();
        services.AddScoped<LauncherService>();
        services.AddScoped<LobbyService>();
        services.AddScoped<MediaService>();
        services.AddScoped<PlaySessionService>();
        services.AddScoped<ProfileService>();
        services.AddScoped<RedistributableService>();
        services.AddScoped<SaveService>();
        services.AddScoped<ScriptService>();
        services.AddScoped<ServerService>();
        services.AddScoped<TagService>();
        
        return services;
    }
}