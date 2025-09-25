using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Configuration;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Rpc;
using LANCommander.SDK.Rpc.Client;
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
        services.AddSingleton<IRpcClient, RpcClient>();
        services.AddScoped<ApiRequestFactory>();
        services.AddScoped<ProcessExecutionContextFactory>();

        services.AddScoped<AuthenticationClient>();
        services.AddSingleton<BeaconClient>();
        services.AddScoped<ChatClient>();
        services.AddSingleton<IConnectionClient, ConnectionClient>();
        services.AddScoped<DepotClient>();
        services.AddScoped<GameClient>();
        services.AddScoped<IssueClient>();
        services.AddScoped<LauncherClient>();
        services.AddScoped<LibraryClient>();
        services.AddScoped<LobbyClient>();
        services.AddScoped<MediaService>();
        services.AddScoped<PlaySessionClient>();
        services.AddScoped<ProfileClient>();
        services.AddScoped<RedistributableClient>();
        services.AddScoped<SaveClient>();
        services.AddScoped<ScriptClient>();
        services.AddScoped<ServerClient>();
        services.AddScoped<TagClient>();

        services.AddScoped<Client>();
        
        return services;
    }
}