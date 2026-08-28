using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class SettingsEndpoints
{
    public static void MapSettingsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Settings");

        group.MapGet("/", GetAsync);
    }
    
    public static async Task<IResult> GetAsync(
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] ServerService serverService)
    {
        var clientSettings = new
        {
            IPXRelay = new
            {
                Host = settingsProvider.CurrentValue.Server.IPXRelay.Host,
                Port = settingsProvider.CurrentValue.Server.IPXRelay.Port,
            },
            Library = new
            {
                EnableUserLibraries = settingsProvider.CurrentValue.Server.Library.EnableUserLibraries,
            },
            Authentication = new
            {
                AllowRegistration = settingsProvider.CurrentValue.Server.Authentication.AllowRegistration,
                AllowPassword = settingsProvider.CurrentValue.Server.Authentication.AllowPassword,
                AutoRedirectToProvider = settingsProvider.CurrentValue.Server.Authentication.AutoRedirectToProvider,
                // Surfaced so the launcher can show the password requirements before the user submits.
                PasswordRequiredLength = settingsProvider.CurrentValue.Server.Authentication.PasswordRequiredLength,
                PasswordRequireDigit = settingsProvider.CurrentValue.Server.Authentication.PasswordRequireDigit,
                PasswordRequireLowercase = settingsProvider.CurrentValue.Server.Authentication.PasswordRequireLowercase,
                PasswordRequireUppercase = settingsProvider.CurrentValue.Server.Authentication.PasswordRequireUppercase,
                PasswordRequireNonAlphanumeric = settingsProvider.CurrentValue.Server.Authentication.PasswordRequireNonAlphanumeric,
            }
        };

        return TypedResults.Ok(clientSettings);
    }
}