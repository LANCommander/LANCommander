using System;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.Configuration;

namespace LANCommander.SDK.Extensions;

public static class IConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddLANCommanderConfiguration(
        this IConfigurationBuilder configurationBuilder,
        out IServerConfigurationRefresher refresher)
    {
        var bootstrap = new ConfigurationBuilder()
            .AddYamlFile(Settings.SETTINGS_FILE_NAME)
            .Build();

        return configurationBuilder
            .AddConfiguration(bootstrap)
            .AddServerConfiguration(bootstrap, out refresher);
    }
    
    public static IConfigurationBuilder AddServerConfiguration(
        this IConfigurationBuilder configurationBuilder,
        IConfiguration configuration,
        out IServerConfigurationRefresher refresher)
    {
        var src = new ServerConfigurationSource
        {
            Configuration = configuration,
        };
        
        configurationBuilder.Add(src);

        refresher = new LazyRefresher(() => src.Provider);

        return configurationBuilder;
    }

    private sealed class LazyRefresher(Func<ServerConfigurationProvider?> getProvider) : IServerConfigurationRefresher
    {
        private readonly Func<ServerConfigurationProvider?> _getProvider = getProvider;

        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            var provider = _getProvider() ??
                throw new InvalidOperationException("Server configuration provider is not yet built");
            
            await provider.RefreshAsync(cancellationToken);
        }
    }
}