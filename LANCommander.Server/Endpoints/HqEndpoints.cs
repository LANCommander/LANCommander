using LANCommander.Server.Services;
using LANCommander.HQ.SDK;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class HqEndpoints
{
    public static void MapHqEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/hq");

        group.MapGet("/callback", CallbackAsync);
        group.MapGet("/status", StatusAsync).RequireAuthorization();
        group.MapPost("/disconnect", DisconnectAsync).RequireAuthorization();
    }

    private static async Task<IResult> CallbackAsync(
        [FromQuery] string? token,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider)
    {
        if (string.IsNullOrWhiteSpace(token))
            return TypedResults.BadRequest("No token provided.");

        settingsProvider.Update(s =>
        {
            s.Server.HQ.AccessToken = token;
            s.Server.HQ.TokenExpiresAt = DateTime.UtcNow.AddHours(24);
        });

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
        [FromServices] HQClient hqClient,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider)
    {
        if (!settingsProvider.CurrentValue.Server.HQ.IsAuthenticated)
            return TypedResults.Ok(new { Connected = false });

        try
        {
            var profile = await hqClient.Auth.GetCurrentUserAsync();

            if (profile is null)
                return TypedResults.Ok(new { Connected = false });

            return TypedResults.Ok(new
            {
                Connected = true,
                profile.Username,
                profile.IsPremium,
                profile.IsEditor,
                profile.PreferredLocale
            });
        }
        catch
        {
            return TypedResults.Ok(new { Connected = false });
        }
    }

    private static async Task<IResult> DisconnectAsync(
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider)
    {
        settingsProvider.Update(s =>
        {
            s.Server.HQ.AccessToken = string.Empty;
            s.Server.HQ.TokenExpiresAt = null;
        });

        return TypedResults.Ok();
    }
}
