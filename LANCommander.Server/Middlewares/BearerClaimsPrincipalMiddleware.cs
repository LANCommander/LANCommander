using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace LANCommander.Server;

public class BearerClaimsPrincipalMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity is null || !context.User.Identity.IsAuthenticated)
        {
            var token = PersonalAccessTokenAuthenticationHandler.GetBearerToken(context.Request);

            if (!String.IsNullOrEmpty(token))
            {
                var scheme = PersonalAccessTokenService.IsPersonalAccessToken(token)
                    ? PersonalAccessTokenAuthenticationHandler.SchemeName
                    : JwtBearerDefaults.AuthenticationScheme;

                var authResult = await context.AuthenticateAsync(scheme);

                if (authResult.Succeeded && authResult.Principal is not null)
                    context.User = authResult.Principal;
                else if (authResult.Failure is PersonalAccessTokenScopeException)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;

                    await context.Response.WriteAsync(authResult.Failure.Message);

                    return;
                }
            }
        }

        await next(context);
    }
}
