using LANCommander.Server.Services;

namespace LANCommander.Server
{
    public class RobotsMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            if (context.Request.Path.StartsWithSegments("/robots.txt"))
            {
                context.Response.ContentType = "text/plain";

                await context.Response.WriteAsync("User-agent: *\nDisallow: /Identity/");
                return;
            }
            
            await next(context);
        }
    }
}
