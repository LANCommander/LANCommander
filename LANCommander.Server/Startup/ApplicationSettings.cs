using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Serilog;

namespace LANCommander.Server.Startup;

public static class ApplicationSettings
{
    public static WebApplicationBuilder AddSettings(this WebApplicationBuilder builder, out Settings settings)
    {
        // Ensure that our default settings are persisted
        if (!File.Exists(SettingService.SettingsFile))
        {
            var workingDirectory = Path.GetDirectoryName(SettingService.SettingsFile);
    
            if (!String.IsNullOrWhiteSpace(workingDirectory))
                Directory.CreateDirectory(workingDirectory);
    
            settings = new Settings();
            SettingService.SaveSettings(settings);
        }
        
        builder.Configuration.AddYamlFile(SettingService.SettingsFile);
        builder.Services.Configure<Settings>(builder.Configuration);
        
        settings = new Settings();
        builder.Configuration.Bind(settings);
        
        // Add services to the container.
        Log.Debug("Loading settings");

        Log.Debug("Validating settings");
        
        if (settings.Authentication.TokenSecret.Length < 16)
        {
            Log.Debug("JWT token secret is too short. Regenerating...");
            settings.Authentication.TokenSecret = Guid.NewGuid().ToString();
            SettingService.SaveSettings(settings);
        }
        
        Log.Debug("Done validating settings");
        
        builder.Services.AddSingleton(settings);

        return builder;
    }
}