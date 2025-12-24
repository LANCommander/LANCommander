using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Rpc.Client;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RpcSubscriber = LANCommander.SDK.Rpc.Clients.RpcSubscriber;

namespace LANCommander.SDK.Extensions;

public static class IServiceCollectionExtensions
{
    public static IServiceCollection AddLANCommanderClient<TSettings>(this IServiceCollection services) where TSettings : Settings, new()
    {
        services.AddSingleton<SettingsProvider<TSettings>>();
        services.AddSingleton<ISettingsProvider>(sp =>
            sp.GetRequiredService<SettingsProvider<TSettings>>());
        
        services.TryAddSingleton<ITokenProvider, TokenProvider>();
        services.TryAddSingleton<INetworkInformationProvider, NetworkInformationProvider>();
        services.TryAddSingleton<IServerAddressProvider, ServerAddressProvider>();
        
        services.AddSingleton<IRpcSubscriber, RpcSubscriber>();
        services.AddSingleton<RpcClient>();
        services.AddSingleton<ApiRequestFactory>();
        services.AddSingleton<ProcessExecutionContextFactory>();
        services.AddSingleton<PowerShellScriptFactory>();

        services.AddSingleton<AuthenticationClient>();
        services.AddSingleton<BeaconClient>();
        services.AddSingleton<ChatHubClient>();
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

        services.AddSingleton<MigrationHistoryService>();
        services.AddSingleton<MigrationService>();
        
        return services;
    }
}