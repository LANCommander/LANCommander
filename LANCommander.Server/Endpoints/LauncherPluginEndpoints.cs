using LANCommander.PluginManagement;
using LANCommander.SDK.Models;
using LANCommander.Server.Plugins.Installation;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

/// <summary>
/// The server's registry of launcher plugins: the list launchers install from, and the packages, laid
/// out like a NuGet flat container (<c>{id}/index.json</c> and <c>{id}/{version}/{id}.{version}.nupkg</c>).
/// </summary>
public static class LauncherPluginEndpoints
{
    public const string Root = "/api/Launcher/Plugins";

    public static void MapLauncherPluginEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Root).RequireAuthorization();

        group.MapGet("/", List);
        group.MapGet("/{id}/index.json", Versions);
        group.MapGet("/{id}/{version}/{fileName}", Download);

        group.MapPost("/Upload", UploadAsync)
            .RequireAuthorization(RoleService.AdministratorRoleName)
            .DisableAntiforgery();
    }

    internal static IResult List([FromServices] LauncherPluginCache cache) =>
        TypedResults.Ok(cache.GetAll().Select(ToModel));

    internal static IResult Versions(string id, [FromServices] LauncherPluginCache cache) =>
        cache.Get(id) is { } plugin
            ? TypedResults.Ok(new { versions = new[] { plugin.Version.ToLowerInvariant() } })
            : TypedResults.NotFound();

    internal static IResult Download(string id, string version, string fileName, [FromServices] LauncherPluginCache cache)
    {
        if (!fileName.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase))
            return TypedResults.NotFound();

        var stream = cache.OpenRead(id, version);

        return stream == null
            ? TypedResults.NotFound()
            : TypedResults.File(stream, "application/octet-stream", fileName.ToLowerInvariant());
    }

    internal static async Task<IResult> UploadAsync(
        IFormFile file,
        [FromServices] LauncherPluginCache cache,
        [FromServices] ILogger<LauncherPluginCache> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();

            var plugin = await cache.AddAsync(stream, cancellationToken);

            return TypedResults.Ok(new { plugin.Id, plugin.Version });
        }
        catch (PluginPackageException ex)
        {
            return TypedResults.Text(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not add the uploaded launcher plugin package {FileName}", file.FileName);

            return TypedResults.Text("The package could not be added. Check the server log for details.", statusCode: StatusCodes.Status400BadRequest);
        }
    }

    internal static LauncherPluginPackage ToModel(CachedLauncherPlugin plugin) => new()
    {
        Id = plugin.Id,
        Version = plugin.Version,
        Title = plugin.Package.Title,
        Description = plugin.Package.Description,
        Authors = plugin.Package.Authors,
        IconUrl = plugin.Package.IconUrl,
        ProjectUrl = plugin.Package.ProjectUrl,
        IsOfficial = plugin.Package.IsOfficial,
        Required = plugin.Required,
        Size = plugin.Size,
        Sha512 = plugin.Sha512,
    };
}
