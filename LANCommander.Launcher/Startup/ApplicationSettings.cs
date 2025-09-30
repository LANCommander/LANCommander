using LANCommander.Launcher.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;
using Serilog;

namespace LANCommander.Launcher.Startup;

public static class ApplicationSettings
{
    public static PhotinoBlazorAppBuilder AddSettings(this PhotinoBlazorAppBuilder builder)
    {
        Log.Debug("Loading settings file");

        IServerConfigurationRefresher refresher;
        
        var configuration = new ConfigurationBuilder()
            .AddLANCommanderConfiguration<Settings>(out refresher)
            .Build();
        
        builder.Services.Configure<Settings>(configuration);
        builder.Services.AddSingleton(refresher);
        
        Log.Debug("Validating settings");

        return builder;
    }
}