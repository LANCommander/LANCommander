using LANCommander.SDK.Extensions;
using LANCommander.Server.ImportExport.Extensions;
using LANCommander.Server.Providers;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Extensions;
using LANCommander.UI.Extensions;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Services
{
    public static WebApplicationBuilder AddLANCommanderServices(this WebApplicationBuilder builder)
    {
        Log.Debug("Registering services");
        
        builder.Services.AddLANCommanderServer();
        builder.Services.AddLANCommanderImportExport();
        builder.Services.AddLANCommanderClient<Settings.Settings>();
        builder.Services.AddLANCommanderUI();

        builder.Services.AddSingleton<IVersionProvider, VersionProvider>();
        
        builder.Services.AddAntDesign();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.AddServiceDefaults();

        return builder;
    }
}