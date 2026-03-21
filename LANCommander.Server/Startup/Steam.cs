using LANCommander.Steam.Extensions;

namespace LANCommander.Server.Startup;

public static class Steam
{
    public static WebApplicationBuilder UseSteam(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.Get<Settings.Settings>();

        builder.Services.AddSteamCmd(o =>
        {
            o.ExecutablePath = settings?.Steam.Path;
        });

        return builder;
    }
}