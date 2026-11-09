using System.Security.Claims;
using LANCommander.SDK.Models;
using LANCommander.Server.Data;
using LANCommander.Server.Services.Events;
using LANCommander.Server.Plugins.Messaging;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Endpoints;

public static class EventEndpoints
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/Events/Launcher", ReportAsync).RequireAuthorization();
    }

    internal static async Task<IResult> ReportAsync(
        LauncherEventReport report,
        ClaimsPrincipal principal,
        IDbContextFactory<DatabaseContext> contextFactory,
        IServerEventPublisher publisher)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Results.Unauthorized();
        var (type, target) = report.Type switch
        {
            LauncherEventType.GameInstalled => (ServerEventTypes.GameInstalled, "game"),
            LauncherEventType.GameInstallFailed => (ServerEventTypes.GameInstallFailed, "game"),
            LauncherEventType.GameUninstalled => (ServerEventTypes.GameUninstalled, "game"),
            LauncherEventType.ToolInstalled => (ServerEventTypes.ToolInstalled, "tool"),
            LauncherEventType.RedistributableInstalled => (ServerEventTypes.RedistributableInstalled, "redistributable"),
            _ => ((string?)null, (string?)null),
        };
        if (type == null)
            return Results.BadRequest("Unsupported launcher event.");

        await using var context = await contextFactory.CreateDbContextAsync();
        if (!await context.Users.AnyAsync(u => u.Id == actorId))
            return Results.Unauthorized();
        var exists = target switch
        {
            "game" => await context.Games!.AnyAsync(g => g.Id == report.TargetId),
            "tool" => await context.Set<Data.Models.Tool>().AnyAsync(t => t.Id == report.TargetId),
            _ => await context.Redistributables!.AnyAsync(r => r.Id == report.TargetId),
        };
        if (!exists)
            return Results.NotFound();

        await publisher.PublishAsync(type, target!, report.TargetId, new { reportedByLauncher = true },
            actorId, status: report.Type == LauncherEventType.GameInstallFailed ? "failed" : "completed");
        return Results.Accepted();
    }
}
