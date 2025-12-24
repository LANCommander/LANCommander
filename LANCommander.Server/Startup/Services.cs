using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Services;
using LANCommander.Server.Clients;
using LANCommander.Server.ImportExport.Extensions;
using LANCommander.Server.Providers;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Providers;
using LANCommander.UI.Extensions;

namespace LANCommander.Server.Startup;

public static class Services
{
    public static WebApplicationBuilder AddLANCommanderServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IServerAddressProvider, ServerAddressProvider>();
        builder.Services.AddSingleton<IVersionProvider, VersionProvider>();
        builder.Services.AddScoped<IChatClient, ServerChatClient>();
        
        builder.Services.AddLANCommanderClient<Settings.Settings>();
        builder.Services.AddLANCommanderServer();
        builder.Services.AddLANCommanderImportExport();
        builder.Services.AddLANCommanderUI();
        
        builder.Services.AddAntDesign();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();
        builder.AddServiceDefaults();

        return builder;
    }
}