using System.Net.Mime;
using LANCommander.Server.Services;
using LANCommander.Server.ImportExport.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class ExportEndpoints
{
    public static void MapExportEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Export");

        group.MapGet("/{contextId:guid}", ExportAsync);
    }

    internal static async Task<IResult> ExportAsync(
        Guid contextId,
        HttpContext httpContext,
        [FromServices] ExportService exportService)
    {
        var context = exportService.GetContext(contextId);

        var syncIOFeature = httpContext.Features.Get<IHttpBodyControlFeature>();

        if (syncIOFeature != null)
            syncIOFeature.AllowSynchronousIO = true;

        httpContext.Response.ContentType = MediaTypeNames.Application.Octet;
        httpContext.Response.Headers.Append("Content-Disposition", "attachment; filename=\"export.lcx\"");

        await exportService.ExportAsync(contextId, httpContext.Response.Body);

        return TypedResults.Empty;
    }
}


