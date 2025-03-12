using Microsoft.AspNetCore.HttpOverrides;

namespace LANCommander.Server.Startup;

public static class Middleware
{
    public static WebApplication UseMiddlewares(this WebApplication app)
    {
        app.UseMiddleware<RobotsMiddleware>();
        app.UseMiddleware<ApiVersioningMiddleware>();
        app.UseMiddleware<PingMiddleware>();
        
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
        });

        return app;
    }
}