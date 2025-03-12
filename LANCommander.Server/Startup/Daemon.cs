namespace LANCommander.Server.Startup;

public static class Daemon
{
    public static WebApplicationBuilder AddAsService(this WebApplicationBuilder builder)
    {
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "LANCommander Server";
        });

        builder.Services.AddSystemd();

        return builder;
    }
}