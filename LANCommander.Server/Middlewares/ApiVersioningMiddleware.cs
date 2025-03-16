using LANCommander.Server.Services;

namespace LANCommander.Server
{
    public class ApiVersioningMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var versionProvider = context.RequestServices.GetService<IVersionProvider>();
            var headers = context.Response.Headers;

            headers.Append("X-API-Version", versionProvider?.GetCurrentVersion().ToString());

            await next(context);
        }
    }
}
