using LANCommander.SDK.Extensions;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Extensions.Options;

namespace LANCommander.Server.Startup;

public static class ApplicationSettings
{
    public static WebApplicationBuilder AddSettings(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration.ReadFromFile<Settings.Settings>();
        var refresher = builder.Configuration.ReadFromServer<Settings.Settings>(configuration);

        builder.Services.AddSingleton(refresher);

        builder.Services.Configure<Settings.Settings>(builder.Configuration);

        // .NET 9's ConfigurationBinder treats IEnumerable<T> as an "immutable array-compatible
        // interface" and calls BindArray, which CONCATENATES the existing default values with the
        // YAML values on every binding. This causes the collections to grow on each reload.
        // The post-configure deduplicates them to prevent unbounded growth.
        builder.Services.AddSingleton<IPostConfigureOptions<Settings.Settings>>(
            new SettingsDeduplicatePostConfigure());

        return builder;
    }
    
    private sealed class SettingsDeduplicatePostConfigure : IPostConfigureOptions<Settings.Settings>
    {
        public void PostConfigure(string? name, Settings.Settings options)
        {
            var launcher = options.Server.Launcher;
            if (launcher.Architectures != null)
                launcher.Architectures = launcher.Architectures.Distinct().ToArray();
            if (launcher.Platforms != null)
                launcher.Platforms = launcher.Platforms.Distinct().ToArray();

            var logs = options.Server.Logs;
            if (logs.Providers != null)
                logs.Providers = logs.Providers.DistinctBy(p => p.Name).ToList();

            var media = options.Server.Media;
            if (media.MediaTypes != null)
                media.MediaTypes = media.MediaTypes.DistinctBy(m => m.Type).ToList();

            var gameServers = options.Server.GameServers;
            if (gameServers.ServerEngines != null)
                gameServers.ServerEngines = gameServers.ServerEngines.DistinctBy(e => e.Name).ToList();
        }
    }

    public static WebApplication ValidateSettings(this WebApplication app)
    {
        var settingsProvider = app.Services.GetRequiredService<SettingsProvider<Settings.Settings>>();
        var settings = app.Services.GetRequiredService<IOptions<Settings.Settings>>();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        
        if (settings.Value.Server.Authentication.TokenSecret.Length < 16)
        {
            logger.LogDebug("JWT token secret is too short. Regenerating...");
            settingsProvider.Update(s =>
            {
                s.Server.Authentication.TokenSecret = Guid.NewGuid().ToString(); 
            });
        }

        return app;
    }
}