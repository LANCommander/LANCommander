using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;

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
    {
        var logger = loggerFactory.CreateLogger("KeysApi");

        try
        {
            Data.Models.Key? key = null;

            var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
            var game = await gameService
                .Include(g => g.Keys)
                .GetAsync(keyRequest.GameId);

            if (game == null)
            {
                logger.LogError("Requested game with ID {GameId} does not exist", keyRequest.GameId);
                return TypedResults.NotFound();
            }

            switch (game.KeyAllocationMethod)
            {
                case KeyAllocationMethod.MacAddress:
                    key = game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.MacAddress &&
                        k.ClaimedByMacAddress == keyRequest.MacAddress);
                    break;

                case KeyAllocationMethod.UserAccount:
                    key = game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.UserAccount &&
                        k.ClaimedByUser?.Id == user?.Id);
                    break;

                default:
                    logger.LogError("Unhandled key allocation method {KeyAllocationMethod}", game.KeyAllocationMethod);
                    return TypedResults.NotFound();
            }

            if (key != null)
                return TypedResults.Ok(mapper.Map<SDK.Models.Key>(key));

            var allocated = await AllocateNewKeyAsync(game.Id, keyRequest, game.KeyAllocationMethod, userPrincipal, mapper, keyService, userService);

            return TypedResults.Ok(allocated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unknown error occurred while trying to get an allocated key for game with ID {GameId}", keyRequest.GameId);

            return TypedResults.NotFound();
        }
    }

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
            Data.Models.Key? key = null;

            var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
            var game = await gameService
                .Include(g => g.Keys)
                .GetAsync(id);

            if (game == null)
            {
                logger.LogError("Requested game with ID {GameId} does not exist", keyRequest.GameId);
                return TypedResults.NotFound();
            }

            switch (game.KeyAllocationMethod)
            {
                case KeyAllocationMethod.MacAddress:
                    key = game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.MacAddress &&
                        k.ClaimedByMacAddress == keyRequest.MacAddress);
                    break;

                case KeyAllocationMethod.UserAccount:
                    key = game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.UserAccount &&
                        k.ClaimedByUser?.Id == user?.Id);
                    break;

                default:
                    logger.LogError("Unhandled key allocation method {KeyAllocationMethod}", game.KeyAllocationMethod);
                    return TypedResults.NotFound();
            }

            if (key != null)
                return TypedResults.Ok(mapper.Map<SDK.Models.Key>(key));

            var allocated = await AllocateNewKeyAsync(id, keyRequest, game.KeyAllocationMethod, userPrincipal, mapper, keyService, userService);

            return TypedResults.Ok(allocated);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unknown error occurred while trying to get an allocated key for game with ID {GameId}", id);

            return TypedResults.NotFound();
        }
    }

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
            Data.Models.Key? key = null;

            var user = await userService.GetAsync(userPrincipal?.Identity?.Name);
            var game = await gameService
                .Include(g => g.Keys)
                .GetAsync(id);

            if (game == null)
            {
                logger.LogError("Requested game with ID {GameId} does not exist", keyRequest.GameId);
                return TypedResults.NotFound();
            }

            switch (game.KeyAllocationMethod)
            {
                case KeyAllocationMethod.MacAddress:
                    key = game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.MacAddress &&
                        k.ClaimedByMacAddress == keyRequest.MacAddress);
                    break;

                case KeyAllocationMethod.UserAccount:
                    key = game.Keys.FirstOrDefault(k =>
                        k.AllocationMethod == KeyAllocationMethod.UserAccount &&
                        k.ClaimedByUser?.Id == user?.Id);
                    break;

                default:
                    logger.LogError("Unhandled key allocation method {KeyAllocationMethod}", game.KeyAllocationMethod);
                    return TypedResults.NotFound();
            }

            var availableKey = game.Keys.FirstOrDefault(k =>
                (k.AllocationMethod == KeyAllocationMethod.MacAddress && string.IsNullOrWhiteSpace(k.ClaimedByMacAddress)) ||
                (k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser == null));

            if (availableKey == null && key != null)
                return TypedResults.Ok(mapper.Map<SDK.Models.Key>(key));

            if (availableKey == null)
                return TypedResults.NotFound();

            if (key != null)
                await keyService.ReleaseAsync(key.Id);

            switch (game.KeyAllocationMethod)
            {
                case KeyAllocationMethod.MacAddress:
                    key = await keyService.AllocateAsync(availableKey, keyRequest.MacAddress);
                    break;

                case KeyAllocationMethod.UserAccount:
                    key = await keyService.AllocateAsync(availableKey, user);
                    break;
            }

            return TypedResults.Ok(mapper.Map<SDK.Models.Key>(key));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unknown error occurred while trying to allocate a new key for game with ID {GameId}", id);

            return TypedResults.NotFound();
        }
    }

    private static async Task<SDK.Models.Key?> AllocateNewKeyAsync(
        Guid id,
        KeyRequest keyRequest,
        KeyAllocationMethod keyAllocationMethod,
        ClaimsPrincipal userPrincipal,
        IMapper mapper,
        KeyService keyService,
        UserService userService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        var keys = await keyService.GetAsync(k => k.GameId == id);
        var availableKey = keys.FirstOrDefault(k =>
            (k.AllocationMethod == KeyAllocationMethod.MacAddress && string.IsNullOrWhiteSpace(k.ClaimedByMacAddress)) ||
            (k.AllocationMethod == KeyAllocationMethod.UserAccount && k.ClaimedByUser == null));

        if (availableKey == null)
            return null;

        if (keyAllocationMethod == KeyAllocationMethod.MacAddress)
            return mapper.Map<SDK.Models.Key>(await keyService.AllocateAsync(availableKey, keyRequest.MacAddress));

        if (keyAllocationMethod == KeyAllocationMethod.UserAccount)
            return mapper.Map<SDK.Models.Key>(await keyService.AllocateAsync(availableKey, user));

        return null;
    }
}


