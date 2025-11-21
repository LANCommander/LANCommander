using System.Security.Claims;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LANCommander.Server.Endpoints;

public static class ProfileEndpoints
{
    public static void MapProfileEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Profile").RequireAuthorization();

        group.MapGet("/", GetAsync);
        group.MapPut("/ChangeAlias", ChangeAliasAsync);
        group.MapGet("/Avatar", AvatarAsync);
        group.MapGet("/{userName}/Avatar", AvatarByUserNameAsync).AllowAnonymous();
        group.MapGet("/CustomField/{name}", GetCustomFieldAsync);
        group.MapPut("/CustomField/{name}", UpdateCustomFieldAsync);
    }

    internal static async Task<IResult> GetAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService)
    {
        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
        {
            var user = await userService.GetAsync<SDK.Models.User>(userPrincipal.Identity!.Name);

            if (user != null)
                return TypedResults.Ok(user);

            return TypedResults.NotFound();
        }

        return TypedResults.Unauthorized();
    }

    internal static async Task<IResult> ChangeAliasAsync(
        [FromBody] ChangeAliasRequest request,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService)
    {
        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
        {
            var user = await userService.GetAsync(userPrincipal.Identity!.Name);

            user.Alias = request.Alias;

            await userService.UpdateAsync(user);

            return TypedResults.Ok(request.Alias);
        }

        return TypedResults.Unauthorized();
    }

    internal static async Task<IResult> AvatarAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] MediaService mediaService)
    {
        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
        {
            var user = await userService.GetAsync(userPrincipal.Identity!.Name);

            if (user == null)
                return TypedResults.NotFound();

            var media = await mediaService.FirstOrDefaultAsync(
                m => m.Type == SDK.Enums.MediaType.Avatar && m.UserId == user.Id);

            if (media == null)
                return TypedResults.NotFound();

            var fs = File.OpenRead(MediaService.GetMediaPath(media));

            return TypedResults.File(fs, media.MimeType);
        }

        return TypedResults.NotFound();
    }

    internal static async Task<IResult> AvatarByUserNameAsync(
        string userName,
        [FromServices] UserService userService,
        [FromServices] MediaService mediaService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ProfileApi");

        try
        {
            var user = await userService.GetAsync(userName);

            if (user == null)
                return TypedResults.NotFound();

            var media = await mediaService.FirstOrDefaultAsync(
                m => m.Type == SDK.Enums.MediaType.Avatar && m.UserId == user.Id);

            if (media == null)
                return TypedResults.NotFound();

            var fs = File.OpenRead(MediaService.GetMediaPath(media));

            return TypedResults.File(fs, media.MimeType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while getting avatar for user {UserName}", userName);
            return TypedResults.NotFound();
        }
    }

    internal static async Task<IResult> GetCustomFieldAsync(
        string name,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] UserCustomFieldService userCustomFieldService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ProfileApi");

        try
        {
            var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

            var field = await userCustomFieldService.GetAsync(user.Id, name);

            return TypedResults.Ok(field.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not get the custom field with the name {CustomFieldName}", name);

            return TypedResults.NotFound();
        }
    }

    internal static async Task<IResult> UpdateCustomFieldAsync(
        string name,
        [FromBody] string value,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] UserCustomFieldService userCustomFieldService,
        [FromServices] ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("ProfileApi");

        try
        {
            var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

            await userCustomFieldService.UpdateAsync(user.Id, name, value);

            return TypedResults.Ok(value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update the custom field with the name {CustomFieldName}", name);

            return TypedResults.BadRequest();
        }
    }
}


