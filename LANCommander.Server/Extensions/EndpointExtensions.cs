using LANCommander.Server.Services;

namespace LANCommander.Server;

public static class EndpointExtensions
{
    public static void UseRobots(this WebApplication app) =>
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/robots.txt"))
            {
                context.Response.ContentType = "text/plain";

                await context.Response.WriteAsync("User-agent: *\nDisallow: /Identity/");
                return;
            }
            
            await next();
        });

    public static void UseApiVersioning(this WebApplication app) =>
        app.Use((context, next) =>
        {
            var headers = context.Response.Headers;

            headers.Append("X-API-Version", UpdateService.GetCurrentVersion().ToString());

            return next();
        });
}
