using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Semver;

namespace LANCommander.Server.Endpoints;

public static class LauncherEndpoints
{
    public static void MapLauncherEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Launcher").RequireAuthorization();

        group.MapGet("/Download", DownloadAsync).AllowAnonymous();
        group.MapGet("/CheckForUpdate", CheckForUpdateAsync);
    }

    internal static async Task<IResult> DownloadAsync(
        [FromServices] IVersionProvider versionProvider,
        [FromServices] IGitHubService gitHubService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider)
    {
        var version = versionProvider.GetCurrentVersion();
        var fileName = $"LANCommander.Launcher-Windows-x64-v{version.WithoutMetadata()}.zip";
        var path = Path.Combine(settingsProvider.CurrentValue.Server.Launcher.StoragePath, fileName);

        if (!File.Exists(path) || !settingsProvider.CurrentValue.Server.Launcher.HostUpdates)
        {
            var release = await gitHubService.GetReleaseAsync(version);

            if (release == null)
                return TypedResults.NotFound();

            var asset = release.Assets.FirstOrDefault(a => a.Name == fileName);

            if (asset != null)
                return TypedResults.Redirect(asset.BrowserDownloadUrl);

            return TypedResults.NotFound();
        }

        return TypedResults.File(
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            fileName);
    }

    internal static async Task<IResult> CheckForUpdateAsync(
        HttpRequest request,
        LinkGenerator linkGenerator,
        [FromServices] IVersionProvider versionProvider)
    {
        var response = new CheckForUpdateResponse();
        var launcherVersionString = request.Headers["X-API-Version"].ToString();

        if (SemVersion.TryParse(launcherVersionString, SemVersionStyles.Any, out var launcherVersion))
        {
            var currentVersion = versionProvider.GetCurrentVersion();

            if (launcherVersion.ComparePrecedenceTo(currentVersion) < 0)
            {
                response.UpdateAvailable = true;
                response.Version = currentVersion.ToString();

                // Generate a URL equivalent to Url.Action("Download", "Launcher")
                response.DownloadUrl = linkGenerator.GetPathByAction(
                    httpContext: null,
                    action: "Download",
                    controller: "Launcher");
            }
        }

        return TypedResults.Ok(response);
    }
}


