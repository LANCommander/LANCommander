using LANCommander.Server.Services;
using LANCommander.Server.Services.HQ;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LANCommander.Server.Endpoints;

public static class HqEndpoints
{
    public static void MapHqEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/HQ")
            .RequireAuthorization(policy => policy.RequireRole(RoleService.AdministratorRoleName));

        // The callback writes the server's HQ credential, so it is guarded exactly like the settings
        // page that starts the flow. It runs as a top-level navigation in a popup, so the session
        // cookie is sent under SameSite=Lax.
        group.MapGet("/Callback", CallbackAsync);
        group.MapGet("/Status", StatusAsync);
        group.MapPost("/Disconnect", DisconnectAsync);
    }

    private static async Task<IResult> CallbackAsync(
        [FromQuery] string? code,
        ClaimsPrincipal user,
        [FromServices] HqConnectionService hqConnection,
        [FromServices] HqAuthorizationStateStore stateStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return TypedResults.BadRequest("No authorization code provided.");

        // Single-use, and only for an administrator who clicked Connect on this server in the last
        // few minutes. Without it, an admin could be walked through a login of someone else's
        // choosing and the server would silently adopt that account's credential.
        if (!stateStore.TryConsume(user.Identity?.Name))
            return TypedResults.BadRequest("The LANCommander HQ authorization request has expired or is invalid. Start again from the HQ settings page.");

        // Exchanges the code, persists the resulting token pair, and verifies against HQ before we
        // reply — so the page that opened this popup can react to a real result rather than poll
        // for a changed token string.
        await hqConnection.AcceptAuthorizationCodeAsync(code, cancellationToken);

        var html = """
            <!DOCTYPE html>
            <html>
            <head><title>Connected to LANCommander HQ</title></head>
            <body>
                <p>Successfully connected to LANCommander HQ. You may close this window.</p>
                <script>
                    if (window.opener) {
                        window.opener.postMessage('hq-connected', '*');
                        window.close();
                    }
                </script>
            </body>
            </html>
            """;

        return TypedResults.Content(html, "text/html");
    }

    private static async Task<IResult> StatusAsync(
        [FromQuery] bool refresh,
        [FromServices] HqConnectionService hqConnection,
        CancellationToken cancellationToken)
    {
        if (refresh)
            await hqConnection.VerifyAsync(cancellationToken);

        return TypedResults.Ok(hqConnection.Current);
    }

    private static async Task<IResult> DisconnectAsync(
        [FromServices] HqConnectionService hqConnection,
        CancellationToken cancellationToken)
    {
        await hqConnection.DisconnectAsync(cancellationToken);

        return TypedResults.Ok();
    }
}
