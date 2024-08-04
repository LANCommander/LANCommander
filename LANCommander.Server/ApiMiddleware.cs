using LANCommander.Server.Services;

namespace LANCommander.Server
{
    public class ApiMiddleware
    {
        private readonly RequestDelegate Next;

        public ApiMiddleware(RequestDelegate next)
        {
            Next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-API-Version"] = UpdateService.GetCurrentVersion().ToString();

                return Task.CompletedTask;
            });

            await Next(context);
        }
    }
}
