using System.Security.Claims;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Models;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
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
        group.MapPost("/Avatar", UploadAvatarAsync).DisableAntiforgery();
        group.MapDelete("/Avatar", RemoveAvatarAsync);
        group.MapGet("/{userName}/Avatar", AvatarByUserNameAsync).AllowAnonymous();
        group.MapGet("/CustomField/{name}", GetCustomFieldAsync);
        group.MapPut("/CustomField/{name}", UpdateCustomFieldAsync);
    }

    internal static async Task<IResult> GetAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] UserService userService)
    {
        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
        {
            var user = await userService.GetAsync(userPrincipal.Identity!.Name, sdkMapper.ProjectToSdkUser);

            if (user != null)
                return TypedResults.Ok(user);

            return TypedResults.NotFound();
        }

        return TypedResults.Unauthorized();
    }

    internal static async Task<IResult> ChangeAliasAsync(
        [FromBody] ChangeAliasRequest request,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider)
    {
        if (!settingsProvider.CurrentValue.Server.Authentication.AllowAliasChange)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        if (userPrincipal?.Identity?.IsAuthenticated ?? false)
        {
            var user = await userService.GetAsync(userPrincipal.Identity!.Name);

            if (user == null)
                return TypedResults.NotFound();

            // A blank alias clears it, so the player goes by their username again
            var alias = request.Alias?.Trim();

            user.Alias = String.IsNullOrEmpty(alias) ? null : alias;

            await userService.UpdateAsync(user);

            return TypedResults.Ok(user.Alias ?? String.Empty);
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

    internal static async Task<IResult> UploadAvatarAsync(
        IFormFile file,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] MediaService mediaService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!settingsProvider.CurrentValue.Server.Authentication.AllowAvatarUpload)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        if (user == null)
            return TypedResults.Unauthorized();

        var config = settingsProvider.CurrentValue.Server.Media.GetMediaTypeConfig(SDK.Enums.MediaType.Avatar);

        if (config != null && config.MaxFileSize > 0 && file.Length > config.MaxFileSize)
            return TypedResults.Text(
                $"The image must be smaller than {ByteSizeLib.ByteSize.FromBytes(config.MaxFileSize)}.",
                statusCode: StatusCodes.Status413PayloadTooLarge);

        try
        {
            await using var stream = file.OpenReadStream();

            // Decided by the file's contents, not the client's say-so
            var mimeType = await MediaService.DetectImageMimeTypeAsync(stream, cancellationToken);

            if (mimeType == null || !MediaService.AvatarMimeTypes.Contains(mimeType))
                return TypedResults.Text(
                    "Avatars must be PNG, JPEG, or WebP images.",
                    statusCode: StatusCodes.Status415UnsupportedMediaType);

            var media = await mediaService.SetUserAvatarAsync(user.Id, stream, mimeType);

            return TypedResults.Ok(new { media.Crc32 });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            loggerFactory.CreateLogger("ProfileApi").LogError(ex, "Could not save the avatar uploaded by {UserName}", user.UserName);

            return TypedResults.Text(
                "The avatar could not be saved. Check the server log for details.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    internal static async Task<IResult> RemoveAvatarAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] MediaService mediaService,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider)
    {
        if (!settingsProvider.CurrentValue.Server.Authentication.AllowAvatarUpload)
            return TypedResults.StatusCode(StatusCodes.Status403Forbidden);

        var user = await userService.GetAsync(userPrincipal?.Identity?.Name);

        if (user == null)
            return TypedResults.Unauthorized();

        await mediaService.RemoveUserAvatarAsync(user.Id);

        return TypedResults.NoContent();
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


