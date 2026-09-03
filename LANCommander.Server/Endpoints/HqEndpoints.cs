using LANCommander.Server.Services;
using LANCommander.Server.Services.HQ;
using Microsoft.AspNetCore.Mvc;
using System.Net;
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
        var snapshot = await hqConnection.AcceptAuthorizationCodeAsync(code, cancellationToken);

        return TypedResults.Content(ResultPage(snapshot), "text/html");
    }

    /// <summary>
    /// The page the popup lands on, reporting what actually happened.
    /// </summary>
    /// <remarks>
    /// This used to announce success unconditionally, discarding the snapshot it was handed. A
    /// failed exchange therefore left the administrator reading "Successfully connected" while the
    /// server logged the opposite — the single most confusing way to fail.
    /// </remarks>
    private static string ResultPage(HqConnectionSnapshot snapshot)
    {
        var connected = snapshot.Status == HqConnectionStatus.Connected;

        var title = connected
            ? "Connected to LANCommander HQ"
            : "Could not connect to LANCommander HQ";

        var message = connected
            ? "Successfully connected to LANCommander HQ. You may close this window."
            : "Could not connect to LANCommander HQ. Check the server logs for details.";

        // LastError is built from an HQ response, so it is not ours to trust into markup.
        var detail = connected || string.IsNullOrWhiteSpace(snapshot.LastError)
            ? string.Empty
            : $"<p>{WebUtility.HtmlEncode(snapshot.LastError)}</p>";

        // Only a success closes itself. A failure stays open so the reason can be read.
        var script = connected
            ? "if (window.opener) { window.opener.postMessage('hq-connected', '*'); window.close(); }"
            : "if (window.opener) { window.opener.postMessage('hq-failed', '*'); }";

        return $"""
            <!DOCTYPE html>
            <html>
            <head><title>{title}</title></head>
            <body>
                <p>{message}</p>
                {detail}
                <script>
                    {script}
                </script>
            </body>
            </html>
            """;
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
