using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Mvc;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class ServerEndpoints
{
    public static void MapServerEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Server");

        group.MapGet("/", GetAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/Status", GetStatusAsync);
        group.MapPost("/{id:guid}/Start", StartAsync);
        group.MapPost("/{id:guid}/Stop", StopAsync);
    }
    
    public static async Task<IResult> GetAsync(
        [FromServices] ServerService serverService,
        [FromServices] SdkMapper sdkMapper)
    {
        var servers = (await serverService.GetAsync()).Select(sdkMapper.ToSdk).ToList();

        return TypedResults.Ok(servers);
    }

    public static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] ServerService serverService,
        [FromServices] SdkMapper sdkMapper)
    {
        var server = await serverService.GetAsync(id);

        if (server == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(sdkMapper.ToSdk(server));
    }

    public static async Task<IResult> GetStatusAsync(
        Guid id,
        [FromServices] ServerManager serverManager)
    {
        return TypedResults.Ok(await serverManager.GetStatusAsync(id));
    }

    public static async Task<IResult> StartAsync(
        Guid id,
        [FromServices] ServerManager serverManager)
    {
        try
        {
            await serverManager.StartAsync(id);

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex);
        }
    }

    public static async Task<IResult> StopAsync(
        Guid id,
        [FromServices] ServerManager serverManager)
    {
        try
        {
            await serverManager.StopAsync(id);

            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex);
        }
    }
}