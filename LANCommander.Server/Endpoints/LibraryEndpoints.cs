using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Extensions;
using LANCommander.Server.Data;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Endpoints;

public static class LibraryEndpoints
{
    public static void MapLibraryEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Library").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPost("/AddToLibrary/{id:guid}", AddToLibraryAsync);
        group.MapPost("/RemoveFromLibrary/{id:guid}", RemoveFromLibraryAsync);
        group.MapPost("/RemoveFromLibrary/{id:guid}/Addons", RemoveFromLibraryWithAddonsAsync);
    }

    internal static async Task<IResult> GetAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] IFusionCache cache,
        [FromServices] GameService gameService,
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService,
        [FromServices] DatabaseContext databaseContext,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LibraryApi");

        try
        {
            if (!settingsProvider.CurrentValue.Server.Library.EnableUserLibraries)
            {
                logger.LogInformation("User libraries are disabled, returning all games");
                var games = await cache.GetOrSetAsync<IEnumerable<SDK.Models.Game>>(
                    "Games",
                    async _ =>
                    {
                        logger.LogDebug("Mapped games cache is empty, repopulating");

                        var entities = await gameService
                            .AsNoTracking()
                            .AsSplitQuery()
                            .GetAsync();

                        return mapper.Map<IEnumerable<SDK.Models.Game>>(entities);
                    },
                    TimeSpan.MaxValue,
                    tags: ["Games"]);

                var ordered = games.OrderByTitle(g => string.IsNullOrWhiteSpace(g.SortTitle) ? g.Title : g.SortTitle);

                return TypedResults.Ok(ordered.Select(g => mapper.Map<SDK.Models.EntityReference>(g)));
            }

            if (userPrincipal.Identity?.Name is null)
            {
                logger.LogError("Failed to get user library: User is not authenticated");
                return TypedResults.Ok(Enumerable.Empty<SDK.Models.EntityReference>());
            }

            logger.LogDebug("Getting library for user {User}", userPrincipal.Identity.Name);

            var user = await userService.GetAsync(userPrincipal.Identity.Name);

            var result = await cache.GetOrSetAsync(
                $"Library/{user.Id}",
                async _ =>
                {
                    var library = await libraryService.GetByUserIdAsync(user.Id);
                    var libraryGameIds = library.Games.Select(g => g.Id).ToList();

                    var games = await databaseContext.Games
                        .AsNoTracking()
                        .AsSplitQuery()
                        .Where(g => libraryGameIds.Contains(g.Id))
                        .ToListAsync();

                    var ordered = games.OrderByTitle(g => string.IsNullOrWhiteSpace(g.SortTitle) ? g.Title : g.SortTitle);

                    return mapper.Map<IEnumerable<SDK.Models.EntityReference>>(ordered);
                },
                TimeSpan.MaxValue,
                tags: ["Library"]);

            return TypedResults.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get user library for user {User}", userPrincipal.Identity?.Name ?? "Unknown User");
            return TypedResults.Ok(Enumerable.Empty<SDK.Models.EntityReference>());
        }
    }

    internal static async Task<IResult> AddToLibraryAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LibraryApi");

        try
        {
            if (userPrincipal.Identity?.Name is null)
            {
                logger.LogError("Failed to add game {GameId} to library: User is not authenticated", id);
                return TypedResults.Ok(false);
            }

            var user = await userService.GetAsync(userPrincipal.Identity.Name);

            logger.LogDebug("Adding game {GameId} to library for user {User}", id, userPrincipal.Identity.Name);
            await libraryService.AddToLibraryAsync(user.Id, id);

            await cache.ExpireAsync($"Library/{user.Id}");

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add game {GameId} to library for user {User}", id, userPrincipal.Identity?.Name ?? "Unknown User");
            return TypedResults.Ok(false);
        }
    }

    internal static Task<IResult> RemoveFromLibraryAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        return RemoveFromLibraryWithAddonsAsync(
            id,
            SDK.Models.GenericGuidsRequest.Empty,
            userPrincipal,
            libraryService,
            userService,
            cache,
            loggerFactory);
    }

    internal static async Task<IResult> RemoveFromLibraryWithAddonsAsync(
        Guid id,
        [FromBody] SDK.Models.GenericGuidsRequest addonGuids,
        ClaimsPrincipal userPrincipal,
        [FromServices] LibraryService libraryService,
        [FromServices] UserService userService,
        [FromServices] IFusionCache cache,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("LibraryApi");

        try
        {
            if (userPrincipal.Identity?.Name is null)
            {
                logger.LogError("Failed to remove game {GameId} from library: User is not authenticated", id);
                return TypedResults.Ok(false);
            }

            var user = await userService.GetAsync(userPrincipal.Identity.Name);

            logger.LogDebug("Removing game {GameId} from library for user {User}", id, userPrincipal.Identity.Name);

            await libraryService.RemoveFromLibraryAsync(user.Id, id, addonGuids?.Guids ?? []);

            await cache.ExpireAsync($"Library/{user.Id}");

            return TypedResults.Ok(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove game {GameId} from library for user {User}", id, userPrincipal.Identity?.Name ?? "Unknown User");
            return TypedResults.Ok(false);
        }
    }
}


