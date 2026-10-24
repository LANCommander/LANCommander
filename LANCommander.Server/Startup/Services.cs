using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Services;
using LANCommander.Server.Clients;
using LANCommander.Server.ImportExport.Extensions;
using LANCommander.Server.Plugins;
using LANCommander.Server.Plugins.Installation;
using LANCommander.Server.Providers;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Providers;
using LANCommander.Server.UI.Extensions;

namespace LANCommander.Server.Startup;

public static class Services
{
    public static WebApplicationBuilder AddLANCommanderServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IServerAddressProvider, ServerAddressProvider>();
        builder.Services.AddSingleton<IVersionProvider, VersionProvider>();
        builder.Services.AddScoped<IChatClient, ServerChatClient>();
        builder.Services.AddScoped<LANCommander.Server.Endpoints.DownloadThrottle>();
        
        builder.Services.AddLANCommanderClient<Settings.Settings>();
        builder.Services.AddLANCommanderServer();
        builder.Services.AddLANCommanderImportExport();
        builder.Services.AddLANCommanderServerUI();

        // What the server offers plugins through LANCommander.Server.Plugins, so they never need
        // its service or data assemblies
        builder.Services.AddScoped<IPackageImporter, PackageImporter>();
        builder.Services.AddScoped<IStorageLocationProvider, StorageLocationProvider>();

        // And what plugins offer the server: their media sources join the media search
        builder.Services.AddScoped<IMediaGrabberProvider, PluginMediaGrabbers>();

        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.AddServiceDefaults();

        // Plugins installed or removed from the plugins page since the last start are moved into
        // place before discovery, while none of their files are loaded
        var pluginStaging = PluginStaging.CreateDefault();

        using (var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole()))
            pluginStaging.ApplyPending(loggerFactory.CreateLogger("LANCommander.Plugins"));

        builder.Services.AddSingleton(pluginStaging);
        builder.Services.AddSingleton<PluginPackageService>();

        // Plugin framework: discover drop-in plugins and let them register services. Must run last,
        // while the service collection is still open (the provider is built immediately after).
        PluginBootstrap.ConfigurePlugins(builder.Services, PluginHost.Server);

        return builder;
    }
}