using System.Security.Claims;
using AutoMapper;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Endpoints;

public static class PlaySessionsEndpoints
{
    public static void MapPlaySessionsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/PlaySessions").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPost("/Start/{id:guid}", StartAsync);
        group.MapPost("/End/{id:guid}", EndAsync);
        group.MapGet("/{id:guid}", GetByGameAsync);
    }

    internal static async Task<IResult> StartAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] PlaySessionService playSessionService,
        [FromServices] GameService gameService,
        [FromServices] UserService userService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
        var game = await gameService.GetAsync(id);

        if (game == null || user == null)
            return TypedResults.BadRequest();

        var activeSessions = await playSessionService
            .Include(ps => ps.Game)
            .GetAsync(ps => ps.UserId == user.Id && ps.End == null);

        foreach (var activeSession in activeSessions)
            await playSessionService.EndSessionAsync(game.Id, activeSession.UserId);

        await playSessionService.StartSessionAsync(game.Id, user.Id);

        return TypedResults.Ok();
    }

    internal static async Task<IResult> EndAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] PlaySessionService playSessionService,
        [FromServices] GameService gameService,
        [FromServices] UserService userService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
        var game = await gameService.GetAsync(id);

        if (game == null || user == null)
            return TypedResults.BadRequest();

        await playSessionService.EndSessionAsync(game.Id, user.Id);

        return TypedResults.Ok();
    }

    internal static async Task<IResult> GetAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] PlaySessionService playSessionService,
        [FromServices] UserService userService,
        [FromServices] IMapper mapper)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        if (user == null)
            return TypedResults.Unauthorized();

        var sessions = await playSessionService.Query(q =>
        {
            return q.AsNoTracking();
        }).GetAsync(ps => ps.UserId == user.Id);

        return TypedResults.Ok(mapper.Map<IEnumerable<SDK.Models.PlaySession>>(sessions));
    }

    internal static async Task<IResult> GetByGameAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] PlaySessionService playSessionService,
        [FromServices] UserService userService,
        [FromServices] IMapper mapper)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        if (user == null)
            return TypedResults.Unauthorized();

        var sessions = await playSessionService.Query(q =>
        {
            return q.AsNoTracking();
        }).GetAsync(ps => ps.UserId == user.Id && ps.GameId == id);

        return TypedResults.Ok(mapper.Map<IEnumerable<SDK.Models.PlaySession>>(sessions));
    }
}


