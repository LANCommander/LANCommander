using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Rpc;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LANCommander.SDK.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderClient<TSettings>(this IServiceCollection services) where TSettings : Settings, new()
    {
        IServerConfigurationRefresher configRefresher;

        var configuration = new ConfigurationBuilder()
            .AddLANCommanderConfiguration<TSettings>(out configRefresher)
            .Build();

        services.AddSingleton(configRefresher);
        
        services.AddSingleton<SettingsProvider<TSettings>>();
        services.AddSingleton<ISettingsProvider>(sp =>
            sp.GetRequiredService<SettingsProvider<TSettings>>());
        
        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddSingleton<INetworkInformationProvider, NetworkInformationProvider>();
        
        services.AddSingleton<IRpcClient, RpcClient>();
        services.AddSingleton<ApiRequestFactory>();
        services.AddSingleton<ProcessExecutionContextFactory>();

        services.AddSingleton<AuthenticationClient>();
        services.AddSingleton<BeaconClient>();
        services.AddSingleton<ChatClient>();
        services.AddSingleton<IConnectionClient, ConnectionClient>();
        services.AddSingleton<DepotClient>();
        services.AddSingleton<GameClient>();
        services.AddSingleton<IssueClient>();
        services.AddSingleton<LauncherClient>();
        services.AddSingleton<LibraryClient>();
        services.AddSingleton<LobbyClient>();
        services.AddSingleton<MediaClient>();
        services.AddSingleton<PlaySessionClient>();
        services.AddSingleton<ProfileClient>();
        services.AddSingleton<RedistributableClient>();
        services.AddSingleton<SaveClient>();
        services.AddSingleton<ScriptClient>();
        services.AddSingleton<ServerClient>();
        services.AddSingleton<TagClient>();

        services.AddSingleton<Client>();
        
        return services;
    }
}