using LANCommander.SDK.Models;
using LANCommander.SDK.Updating;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Semver;

namespace LANCommander.Server.Endpoints;

public static class LauncherEndpoints
{
    // Launchers that predate platform-aware updates send no query and were only built for Windows x64
    private const string DefaultPlatform = "Windows";
    private const string DefaultArchitecture = "x64";

    public static void MapLauncherEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Launcher").RequireAuthorization();

        group.MapGet("/Download", DownloadAsync).AllowAnonymous();
        group.MapGet("/CheckForUpdate", CheckForUpdateAsync);
    }

    internal static async Task<IResult> DownloadAsync(
        [FromQuery] string? platform,
        [FromQuery] string? architecture,
        [FromQuery] UpdatePackageKind? package,
        [FromServices] UpdateService updateService)
    {
        var update = await updateService.FindLauncherUpdateAsync(
            platform ?? DefaultPlatform,
            architecture ?? DefaultArchitecture,
            package ?? UpdatePackageKind.Archive);

        if (update == null)
            return TypedResults.NotFound();

        if (update.LocalPath == null)
            return TypedResults.Redirect(update.Artifact.Url);

        return TypedResults.File(
            new FileStream(update.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read),
            "application/octet-stream",
            update.FileName);
    }

    internal static async Task<IResult> CheckForUpdateAsync(
        HttpRequest request,
        [FromQuery] string? platform,
        [FromQuery] string? architecture,
        [FromQuery] UpdatePackageKind? package,
        [FromServices] UpdateService updateService)
    {
        var response = new CheckForUpdateResponse();
        var launcherVersionString = request.Headers["X-API-Version"].ToString();

        if (!SemVersion.TryParse(launcherVersionString, SemVersionStyles.Any, out var launcherVersion))
            return TypedResults.Ok(response);

        platform ??= DefaultPlatform;
        architecture ??= DefaultArchitecture;
        package ??= UpdatePackageKind.Archive;

        var update = await updateService.FindLauncherUpdateAsync(platform, architecture, package.Value);

        if (update == null || launcherVersion.ComparePrecedenceTo(update.Version) >= 0)
            return TypedResults.Ok(response);

        response.UpdateAvailable = true;
        response.Version = update.Version.ToString();
        response.FileName = update.FileName;
        response.DownloadUrl = $"/api/Launcher/Download?platform={Uri.EscapeDataString(platform)}&architecture={Uri.EscapeDataString(architecture)}&package={package}";

        if (update.LocalPath != null)
        {
            response.Sha256 = await updateService.GetFileHashAsync(update.LocalPath);
            response.Size = new FileInfo(update.LocalPath).Length;
        }

        return TypedResults.Ok(response);
    }
}
