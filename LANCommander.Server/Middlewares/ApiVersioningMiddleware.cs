using LANCommander.Server.Services;

namespace LANCommander.Server
{
    public class ApiVersioningMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var headers = context.Response.Headers;

            headers.Append("X-API-Version", UpdateService.GetCurrentVersion().ToString());

            await next(context);
        }
    }
}
