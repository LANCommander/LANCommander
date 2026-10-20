using LANCommander.SDK.Helpers;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class ScriptEndpoints
{
    public static void MapScriptEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Scripts");

        group.MapPost("/{id:guid}/Contents", UpdateContentsAsync)
            .RequireAuthorization(RoleService.AdministratorRoleName);
    }

    /// <summary>
    /// Replace a script's contents. Used by the launcher's script debugger to publish edits made while
    /// debugging. Everything else about the script is left as it is.
    /// </summary>
    internal static async Task<IResult> UpdateContentsAsync(
        Guid id,
        [FromBody] SDK.Models.UpdateScriptContentsRequest request,
        [FromServices] ScriptService scriptService,
        [FromServices] SdkMapper sdkMapper)
    {
        if (request?.Contents is null)
            return TypedResults.BadRequest();

        var existing = await scriptService.GetAsync(id);

        if (existing == null)
            return TypedResults.NotFound();

        if (!string.IsNullOrEmpty(request.BaseContentsHash)
            && !string.Equals(request.BaseContentsHash, ScriptHelper.HashContents(existing.Contents), StringComparison.OrdinalIgnoreCase))
            return TypedResults.Conflict();

        existing.Contents = request.Contents;

        existing = await scriptService.UpdateAsync(existing);

        return TypedResults.Ok(sdkMapper.ToSdk(existing));
    }
}
