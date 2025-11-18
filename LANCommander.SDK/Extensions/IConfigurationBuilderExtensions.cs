using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Settings = LANCommander.SDK.Models.Settings;

namespace LANCommander.SDK.Extensions;

public static class IConfigurationBuilderExtensions
{
    public static IConfigurationBuilder AddLANCommanderConfiguration<TSettings>(
        this IConfigurationBuilder configurationBuilder,
        out IServerConfigurationRefresher refresher) where TSettings : Settings
    {
        var filePath = Path.Join(AppPaths.GetConfigDirectory(), Settings.SETTINGS_FILE_NAME);

        if (!File.Exists(filePath))
        {
            var settings = Activator.CreateInstance<TSettings>();
            
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            File.WriteAllText(filePath, serializer.Serialize(settings));
        }
        
        var bootstrap = new ConfigurationBuilder()
            .AddYamlFile(filePath, false, true)
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