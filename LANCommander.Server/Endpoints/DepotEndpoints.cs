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
        [FromServices] GameService gameService,
        [FromServices] CollectionService collectionService,
        [FromServices] CompanyService companyService,
        [FromServices] EngineService engineService,
        [FromServices] GenreService genreService,
        [FromServices] PlatformService platformService,
        [FromServices] TagService tagService,
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService,
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

        var results = await cache.GetOrSetAsync("Depot/Results", async _ =>
        {
            var games = await gameService
                .AsNoTracking()
                .Include(g => g.Media)
                .Include(g => g.Collections)
                .Include(g => g.Platforms)
                .Include(g => g.Tags)
                .Include(g => g.Developers)
                .Include(g => g.Genres)
                .Include(g => g.Publishers)
                .Include(g => g.Engine)
                .GetAsync();

            var depotResults = new SDK.Models.DepotResults
            {
                Games = mapper.Map<ICollection<SDK.Models.DepotGame>>(games),
                Collections = mapper.Map<ICollection<SDK.Models.Collection>>(await collectionService.AsNoTracking().GetAsync()),
                Companies = mapper.Map<ICollection<SDK.Models.Company>>(await companyService.AsNoTracking().GetAsync()),
                Engines = mapper.Map<ICollection<SDK.Models.Engine>>(await engineService.AsNoTracking().GetAsync()),
                Genres = mapper.Map<ICollection<SDK.Models.Genre>>(await genreService.AsNoTracking().GetAsync()),
                Platforms = mapper.Map<ICollection<SDK.Models.Platform>>(await platformService.AsNoTracking().GetAsync()),
                Tags = mapper.Map<ICollection<SDK.Models.Tag>>(await tagService.AsNoTracking().GetAsync()),
            };

            return depotResults;
        }, TimeSpan.MaxValue, tags: ["Depot"]);

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
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        if (user == null)
            return TypedResults.Unauthorized();

        var game = await cache.GetOrSetAsync($"Depot/Games/{id}", async _ =>
        {
            return await gameService.Query(q =>
                {
                    return q.AsNoTracking();
                })
                .Include(g => g.Actions)
                .Include(g => g.Archives)
                .Include(g => g.BaseGame)
                .Include(g => g.Categories)
                .Include(g => g.Collections)
                .Include(g => g.DependentGames)
                .Include(g => g.Developers)
                .Include(g => g.Engine)
                .Include(g => g.Genres)
                .Include(g => g.Media)
                .Include(g => g.MultiplayerModes)
                .Include(g => g.Platforms)
                .Include(g => g.Publishers)
                .Include(g => g.Redistributables)
                .Include(g => g.SavePaths)
                .Include(g => g.Scripts)
                .Include(g => g.Tags)
                .GetAsync(id);
        }, tags: ["Depot", "Depot/Games", "Games", $"Games/{id}"]);

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


