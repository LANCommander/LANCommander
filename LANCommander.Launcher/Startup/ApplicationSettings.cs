using LANCommander.Launcher.Models;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

namespace LANCommander.Launcher.Startup;

public static class ApplicationSettings
{
    public static PhotinoBlazorAppBuilder AddSettings(this PhotinoBlazorAppBuilder builder)
    {
        var configurationBuilder = new ConfigurationBuilder();

        var configuration = configurationBuilder.ReadFromFile<Settings.Settings>();
        var refresher = configurationBuilder.ReadFromServer<Settings.Settings>(configuration);

        configuration = configurationBuilder.Build();
        
        builder.Services.Configure<Settings.Settings>(configuration);
        builder.Services.AddSingleton(refresher);

        return builder;
    }
}