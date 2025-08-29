using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Serilog;

namespace LANCommander.Launcher.Startup;

public static class ApplicationSettings
{
    public static PhotinoBlazorAppBuilder AddSettings(this PhotinoBlazorAppBuilder builder)
    {
        Settings settings;
        
        if (!File.Exists(SettingService.SettingsFile))
        {
            Log.Debug("Creating settings file");
            
            var workingDirectory = Path.GetDirectoryName(SettingService.SettingsFile);
            
            if (!String.IsNullOrWhiteSpace(workingDirectory))
                Directory.CreateDirectory(workingDirectory);

            settings = new Settings();
            
            SettingService.SaveSettings(settings);
        }
        
        Log.Debug("Loading settings file");

        var configBuilder = new ConfigurationBuilder()
            .AddYamlFile(SettingService.SettingsFile)
            .Build();
        
        builder.Services.Configure<Settings>(configBuilder);

        settings = new Settings();
        configBuilder.Bind(settings);
        
        Log.Debug("Validating settings");

        return builder;
    }
}