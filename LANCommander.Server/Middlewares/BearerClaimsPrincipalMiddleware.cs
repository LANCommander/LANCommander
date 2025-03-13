using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace LANCommander.Server;

public class BearerClaimsPrincipalMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            var authHeader = context.Request.Headers.Authorization.ToString();

            if (!String.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var authResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
                
                if (authResult.Succeeded && authResult.Principal is not null)
                    context.User = authResult.Principal;
            }
        }
        
        await next(context);
    }
}