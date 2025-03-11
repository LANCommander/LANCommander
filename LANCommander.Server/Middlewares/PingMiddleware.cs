using LANCommander.SDK.Extensions;
using LANCommander.Server.Services;

namespace LANCommander.Server
{
    public class PingMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                if (context.Request.Method == "HEAD" && context.Request.Headers.ContainsKey("X-Ping"))
                    context.Response.Headers["X-Pong"] = context.Request.Headers["X-Ping"].First().FastReverse();
            }
            catch
            {
                await next(context);
            }
        }
    }
}
