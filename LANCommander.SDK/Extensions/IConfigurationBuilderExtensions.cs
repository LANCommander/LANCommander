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
    public static IConfiguration ReadFromFile<TSettings>(this IConfigurationBuilder configurationBuilder) where TSettings : Settings, new()
    {
        var filePath = AppPaths.GetConfigPath(Settings.SETTINGS_FILE_NAME);
        
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
        
        configurationBuilder.AddConfiguration(bootstrap);

        return bootstrap;
    }

    public static IServerConfigurationRefresher ReadFromServer<TSettings>(this IConfigurationBuilder configurationBuilder, IConfiguration source)
        where TSettings : Settings, new()
    {
        var src = new ServerConfigurationSource
        {
            Configuration = source,
        };
        
        configurationBuilder.Add(src);

        var refresher = new LazyRefresher(() => src.Provider);

        return refresher;
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