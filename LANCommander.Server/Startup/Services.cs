using LANCommander.Server.Providers;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Services.Extensions;
using LANCommander.Server.Services.Models;
using Serilog;

namespace LANCommander.Server.Startup;

public static class Services
{
    public static WebApplicationBuilder AddLANCommanderServices(this WebApplicationBuilder builder, Settings settings)
    {
        Log.Debug("Registering services");
        
        builder.Services.AddLANCommanderServer(settings);

        builder.Services.AddSingleton<IVersionProvider, VersionProvider>();
        
        builder.Services.AddAntDesign();
        builder.Services.AddHttpClient();
        builder.Services.AddHttpContextAccessor();

        return builder;
    }
}