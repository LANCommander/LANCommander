using LANCommander.PluginManagement;
using LANCommander.Server.Plugins.Installation;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

/// <summary>
/// Plugin package uploads from the plugins page. Not under <see cref="PluginEndpoints.Root"/>, whose
/// segments belong to the plugins' own endpoints.
/// </summary>
public static class PluginPackageEndpoints
{
    public const string Root = "/api/PluginPackages";

    public static void MapPluginPackageEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(Root).RequireAuthorization(RoleService.AdministratorRoleName);

        group.MapPost("/Upload", UploadAsync).DisableAntiforgery();
    }

    internal static async Task<IResult> UploadAsync(
        IFormFile file,
        [FromServices] PluginPackageService pluginPackageService,
        [FromServices] ILogger<PluginPackageService> logger,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();

            var result = await pluginPackageService.InstallAsync(stream, cancellationToken);

            return TypedResults.Ok(new { result.Folder, result.Package.Id, result.Package.Version });
        }
        catch (PluginPackageException ex)
        {
            return TypedResults.Text(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not install the uploaded plugin package {FileName}", file.FileName);

            return TypedResults.Text("The package could not be installed. Check the server log for details.", statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
