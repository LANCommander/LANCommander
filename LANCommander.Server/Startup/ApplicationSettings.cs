using LANCommander.SDK;
using LANCommander.SDK.Extensions;
using Microsoft.Extensions.Options;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Startup;

public static class ApplicationSettings
{
    public static WebApplicationBuilder AddSettings(this WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration.ReadFromFile<Settings.Settings>();
        var refresher = builder.Configuration.ReadFromServer<Settings.Settings>(configuration);
        
        builder.Services.AddSingleton(refresher);
        
        builder.Services.Configure<Settings.Settings>(builder.Configuration);

        return builder;
    }
    
    public static WebApplication ValidateSettings(this WebApplication app)
    {
        var settingsProvider = app.Services.GetRequiredService<SettingsProvider<Settings.Settings>>();
        var settings = app.Services.GetRequiredService<IOptions<Settings.Settings>>();
        
        if (settings.Value.Server.Authentication.TokenSecret.Length < 16)
        {
            Log.Debug("JWT token secret is too short. Regenerating...");
            settingsProvider.Update(s =>
            {
                s.Server.Authentication.TokenSecret = Guid.NewGuid().ToString(); 
            });
        }

        return app;
    }
}