using Hangfire;

namespace LANCommander.Server.Startup;

public static class Hangfire
{
    public static WebApplicationBuilder AddHangfire(this WebApplicationBuilder builder)
    {
        builder.Services.AddHangfire(static (sp, configuration) =>
        {
            var logger = sp.GetRequiredService<ILogger<Program>>();
            logger.LogDebug("Initializing Hangfire");
            configuration
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseInMemoryStorage();
        });
        
        builder.Services.AddHangfireServer();
        
        return builder;
    }

    public static WebApplication UseHangfire(this WebApplication app)
    {
        app.UseHangfireDashboard();
        
        return app;
    }
}