using System.Security.Claims;
using AutoMapper;
using LANCommander.Server.Data;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class DepotEndpoints
{
    public static void MapDepotEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Depot").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapGet("/Games/{id:guid}", GetGamesAsync);
    }

    internal static async Task<IResult> GetAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] IFusionCache cache,
        [FromServices] DepotService depotService,
        [FromServices] UserService userService,
        [FromServices] LibraryService libraryService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("DepotApi");
        var userName = userPrincipal?.Identity?.Name;

        if (string.IsNullOrWhiteSpace(userName))
            return TypedResults.Ok(new SDK.Models.DepotResults());

        var user = await userService
            .Query(q =>
            {
                return q
                    .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role);
            })
            .FirstOrDefaultAsync(u => u.UserName.ToUpper() == userName.ToUpper());

        if (user == null)
            return TypedResults.Ok(new SDK.Models.DepotResults());

        var library = await libraryService.GetByUserIdAsync(user.Id);

        var results = await cache.GetOrSetAsync("Depot/Results", async _ => await depotService.GetResults(), TimeSpan.MaxValue, tags: ["Depot"]);

        results.Popular = await cache.GetOrSetAsync("Depot/Popular", async _ => await depotService.GetPopularGameIds(), TimeSpan.MaxValue, tags: ["Depot", "PlaySessions"]);
        results.Backlog = await cache.GetOrSetAsync("Depot/Backlog", async _ => await depotService.GetBacklogGameIds(), TimeSpan.MaxValue, tags: ["Depot", "PlaySessions", "Ratings"]);

        if (settingsProvider.CurrentValue.Server.Roles.RestrictGamesByCollection)
        {
            var collections = await userService.GetCollectionsAsync(user);

            results.Games = results
                .Games
                .Where(g =>
                    g.Collections.Any(gc =>
                        collections.Any(c => c.Id == gc.Id)))
                .ToList();
        }

        foreach (var game in results.Games)
        {
            game.InLibrary = library.Games.Any(g => g.Id == game.Id);
        }

        if (!results.Games.Any())
        {
            string roles = "No roles";

            if (user?.Roles?.Any() ?? false)
                roles = string.Join(", ", user.Roles.Select(r => r.Name));

            logger.LogInformation(
                "No games found in depot for user {UserName} (Roles: {Roles})",
                user?.UserName,
                roles);
        }

        return TypedResults.Ok(results);
    }

    internal static async Task<IResult> GetGamesAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] IFusionCache cache,
        [FromServices] GameService gameService,
        [FromServices] DepotService depotService,
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        if (user == null)
            return TypedResults.Unauthorized();

        var game = await cache.GetOrSetAsync($"Depot/Games/{id}", async _ => await depotService.GetGameAsync(id), tags: ["Depot", "Depot/Games", "Games", $"Games/{id}"]);

        if (game == null)
            return TypedResults.NotFound();

        var library = await libraryService
            .AsNoTracking()
            .Include(l => l.Games)
            .FirstOrDefaultAsync(l => l.UserId == user.Id);

        var result = mapper.Map<SDK.Models.DepotGame>(game);

        if (library?.Games.Any(g => g.Id == game.Id) ?? false)
            result.InLibrary = true;

        return TypedResults.Ok(result);
    }


}


