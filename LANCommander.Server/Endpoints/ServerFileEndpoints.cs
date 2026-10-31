using LANCommander.Server.Services;
using LANCommander.Server.Services.Utilities;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

/// <summary>Uploads to a game server's files, from its Files page.</summary>
public static class ServerFileEndpoints
{
    public static string UploadUrl(Guid serverId) => $"/api/Server/{serverId}/Files/Upload";

    public static void MapServerFileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Server/{id:guid}/Files").RequireAuthorization(RoleService.AdministratorRoleName);

        group.MapPost("/Upload", UploadAsync).DisableAntiforgery();
    }

    /// <summary>Writes the file into the folder <paramref name="path"/>, replacing one of the same name.</summary>
    internal static async Task<IResult> UploadAsync(
        Guid id,
        IFormFile file,
        [FromForm] string? path,
        [FromServices] ServerManager serverManager,
        [FromServices] ILogger<ServerManager> logger,
        CancellationToken cancellationToken)
    {
        if (!serverManager.SupportsFiles(id))
            return TypedResults.Text("This server's files can't be managed.", statusCode: StatusCodes.Status400BadRequest);

        string target;

        try
        {
            // Browsers send only the name, but some clients send a path; only the name is kept
            target = ServerFilePath.Combine(path, Path.GetFileName(file.FileName.Replace('\\', '/')));

            if (ServerFilePath.GetName(target).Length == 0)
                throw new ArgumentException("The file has no name.");
        }
        catch (ArgumentException ex)
        {
            return TypedResults.Text(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            await using var stream = file.OpenReadStream();

            await serverManager.WriteFileAsync(id, target, stream, cancellationToken);

            return TypedResults.Ok(new { Path = target });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Could not upload {Path} to server {ServerId}", target, id);

            return TypedResults.Text(ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
