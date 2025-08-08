using AutoMapper;
using LANCommander.Server.Services;
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
        [FromServices] IMapper mapper)
    {
        var servers = mapper.Map<IEnumerable<SDK.Models.Server>>(await serverService.GetAsync());

        return TypedResults.Ok(servers);
    }

    public static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] ServerService serverService,
        [FromServices] IMapper mapper)
    {
        var server = await serverService.GetAsync(id);
        
        if (server == null)
            return TypedResults.NotFound();
        
        return TypedResults.Ok(mapper.Map<SDK.Models.Server>(server));
    }

    public static async Task<IResult> GetStatusAsync(
        Guid id,
        [FromServices] ServerService serverService)
    {
        return TypedResults.Ok(await serverService.GetStatusAsync(id));
    }

    public static async Task<IResult> StartAsync(
        Guid id,
        [FromServices] ServerService serverService)
    {
        try
        {
            await serverService.StartAsync(id);
            
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex);
        }
    }
    
    public static async Task<IResult> StopAsync(
        Guid id,
        [FromServices] ServerService serverService)
    {
        try
        {
            await serverService.StopAsync(id);
            
            return TypedResults.Ok();
        }
        catch (Exception ex)
        {
            return TypedResults.BadRequest(ex);
        }
    }
}