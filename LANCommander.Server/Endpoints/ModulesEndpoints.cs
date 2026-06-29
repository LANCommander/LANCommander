using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class ModulesEndpoints
{
    public static void MapModulesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Modules").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("/Download", DownloadAsync);
    }

    internal static IResult GetAsync([FromServices] ModuleService moduleService)
    {
        var names = moduleService.GetModules().Select(m => m.Name);

        return TypedResults.Ok(names);
    }

    internal static IResult DownloadAsync([FromServices] ModuleService moduleService)
    {
        var archivePath = moduleService.GetModulesArchive();

        var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);

        return TypedResults.File(stream, "application/octet-stream", "Modules.zip");
    }
}
