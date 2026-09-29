using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LANCommander.Server.Endpoints;

public static class KeysEndpoints
{
    public static void MapKeysEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Keys").RequireAuthorization();

        group.MapPost("/", GetAsync);
        group.MapPost("/GetAllocated/{id:guid}", GetAllocatedAsync);
        group.MapPost("/Allocate/{id:guid}", AllocateAsync);
    }

    internal static async Task<IResult> GetAsync(
        [FromBody] KeyRequest keyRequest,
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] KeyService keyService,
        [FromServices] GameService gameService,
        [FromServices] UserService userService,
        [FromServices] ILoggerFactory loggerFactory)
        => await GetAllocatedAsync(keyRequest.GameId, keyRequest, userPrincipal, mapper, keyService, gameService, userService, loggerFactory);

    /// <summary>
    /// Returns the key this claimant already holds for the game, allocating one if they do not
    /// hold one yet. Every launcher run funnels through here, so returning the existing claim
    /// rather than a new key is what keeps a game's pool from draining.
    /// </summary>
    internal static async Task<IResult> GetAllocatedAsync(
        Guid id,
        [FromBody] KeyRequest keyRequest,
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] KeyService keyService,
        [FromServices] GameService gameService,
        [FromServices] UserService userService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("KeysApi");

        try
        {
            var request = await ResolveAsync(id, keyRequest, userPrincipal, gameService, userService, logger);

            if (request == null)
                return TypedResults.NotFound();

            if (request.HeldKey != null)
                return TypedResults.Ok(mapper.Map<SDK.Models.Key>(request.HeldKey));

            var availableKey = request.Game.Keys.FirstOrDefault(k => k.IsAvailable());

            if (availableKey == null)
            {
                logger.LogWarning("No keys are available for game with ID {GameId}", id);

                return TypedResults.NotFound();
            }

            return TypedResults.Ok(mapper.Map<SDK.Models.Key>(await AllocateToAsync(keyService, request, availableKey)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unknown error occurred while trying to get an allocated key for game with ID {GameId}", id);

            return TypedResults.NotFound();
        }
    }

    /// <summary>
    /// Moves the claimant onto a different key, returning the one they were holding to the pool.
    /// Falls back to the held key when nothing else is free so a re-roll can never leave a
    /// claimant with no key at all.
    /// </summary>
    internal static async Task<IResult> AllocateAsync(
        Guid id,
        [FromBody] KeyRequest keyRequest,
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] KeyService keyService,
        [FromServices] GameService gameService,
        [FromServices] UserService userService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("KeysApi");

        try
        {
            var request = await ResolveAsync(id, keyRequest, userPrincipal, gameService, userService, logger);

            if (request == null)
                return TypedResults.NotFound();

            var availableKey = request.Game.Keys.FirstOrDefault(k => k.IsAvailable() && k.Id != request.HeldKey?.Id);

            if (availableKey == null && request.HeldKey != null)
                return TypedResults.Ok(mapper.Map<SDK.Models.Key>(request.HeldKey));

            if (availableKey == null)
                return TypedResults.NotFound();

            if (request.HeldKey != null)
                await keyService.ReleaseAsync(request.HeldKey.Id);

            return TypedResults.Ok(mapper.Map<SDK.Models.Key>(await AllocateToAsync(keyService, request, availableKey)));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unknown error occurred while trying to allocate a new key for game with ID {GameId}", id);

            return TypedResults.NotFound();
        }
    }

    /// <summary>
    /// The game, the claimant the request is for, and the key that claimant already holds.
    /// </summary>
    private sealed record KeyAllocationRequest(
        Data.Models.Game Game,
        Data.Models.User? User,
        string? MacAddress,
        Data.Models.Key? HeldKey);

    /// <summary>
    /// Loads the game and works out who the request is claiming for, or returns null when that
    /// cannot be established.
    /// </summary>
    private static async Task<KeyAllocationRequest?> ResolveAsync(
        Guid id,
        KeyRequest keyRequest,
        ClaimsPrincipal userPrincipal,
        GameService gameService,
        UserService userService,
        ILogger logger)
    {
        var game = await gameService
            .Query(q => q.Include(g => g.Keys).ThenInclude(k => k.ClaimedByUser))
            .GetAsync(id);

        if (game == null)
        {
            logger.LogError("Requested game with ID {GameId} does not exist", id);
            return null;
        }

        switch (game.KeyAllocationMethod)
        {
            case KeyAllocationMethod.MacAddress:
                if (String.IsNullOrWhiteSpace(keyRequest.MacAddress))
                {
                    logger.LogError(
                        "Cannot allocate a key for game with ID {GameId}: the game allocates by MAC address but the request did not supply one",
                        id);

                    return null;
                }

                return new KeyAllocationRequest(
                    game,
                    User: null,
                    keyRequest.MacAddress,
                    game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.MacAddress &&
                        k.ClaimedByMacAddress == keyRequest.MacAddress));

            case KeyAllocationMethod.UserAccount:
                var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

                if (user == null)
                {
                    logger.LogError(
                        "Cannot allocate a key for game with ID {GameId}: the game allocates by user account but the requesting user {UserName} could not be resolved",
                        id,
                        userPrincipal?.Identity?.Name);

                    return null;
                }

                return new KeyAllocationRequest(
                    game,
                    user,
                    MacAddress: null,
                    game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.UserAccount &&
                        k.ClaimedByUserId == user.Id));

            default:
                logger.LogError("Unhandled key allocation method {KeyAllocationMethod}", game.KeyAllocationMethod);
                return null;
        }
    }

    private static async Task<Data.Models.Key> AllocateToAsync(
        KeyService keyService,
        KeyAllocationRequest request,
        Data.Models.Key key)
        => request.User != null
            ? await keyService.AllocateAsync(key, request.User)
            : await keyService.AllocateAsync(key, request.MacAddress!);
}
