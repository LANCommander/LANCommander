using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Mappers;
using Microsoft.AspNetCore.Mvc;
using System.DirectoryServices.AccountManagement;
using System.Security.Claims;
using File = System.IO.File;
using SortDirection = LANCommander.Server.Data.Enums.SortDirection;

namespace LANCommander.Server.Endpoints;

public static class SaveEndpoints
{
    public static void MapSaveEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Saves");

        group.MapGet("/", GetAsync);
        group.MapGet("/{id:guid}", GetByIdAsync);
        group.MapGet("/{id:guid}/Download", DownloadSaveByIdAsync);
        group.MapDelete("/{id:guid}", DeleteByIdAsync);
        group.MapGet("/Game/{gameId:guid}", GetSavesByGameAsync);
        group.MapPost("/Game/{gameId:guid}/Upload", UploadSaveByGameAsync);
        group.MapGet("/Game/{gameId:guid}/Latest", GetLatestSaveByGameAsync);
        group.MapGet("/Game/{gameId:guid}/Latest/Download", DownloadLatestSaveByGameAsync);
    }

    public static async Task<IResult> GetAsync(
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);

            return TypedResults.Unauthorized();
        }

        var saves = await saveService.GetAsync(gs => gs.UserId == user.Id, sdkMapper.ProjectToSdkGameSave);

        return TypedResults.Ok(saves);
    }

    public static async Task<IResult> GetByIdAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);

            return TypedResults.Unauthorized();
        }

        var save = await saveService.FirstOrDefaultAsync(s => s.Id == id, sdkMapper.ProjectToSdkGameSave);
        
        if (save == null)
            return TypedResults.NotFound();
        
        return TypedResults.Ok(save);
    }

    public static async Task<IResult> DeleteByIdAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);
            
            return TypedResults.Unauthorized();
        }
        
        var save = await saveService.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id);
        
        if (save == null)
            return TypedResults.NotFound();
        
        await saveService.DeleteAsync(save);
        
        return TypedResults.NoContent();
    }
    
    public static async Task<IResult> GetSavesByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);

            return TypedResults.Unauthorized();
        }

        var userSaves = await saveService.GetAsync(gs => gs.UserId == user.Id && gs.GameId == gameId, sdkMapper.ProjectToSdkGameSave);
        
        return TypedResults.Ok(userSaves);
    }

    public static async Task<IResult> GetLatestSaveByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);

            return TypedResults.Unauthorized();
        }

        var latestSave = await saveService
            .SortBy(s => s.CreatedOn, SortDirection.Descending)
            .FirstOrDefaultAsync(gs => gs.GameId == gameId && gs.UserId == user.Id);
        
        if (latestSave == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(sdkMapper.ToSdk(latestSave));
    }

    public static async Task<IResult> DownloadLatestSaveByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);
            
            return TypedResults.Unauthorized();
        }
        
        var latestSave = await saveService
            .SortBy(s => s.CreatedOn, SortDirection.Descending)
            .Include(gs => gs.Game)
            .Include(gs => gs.StorageLocation)
            .FirstOrDefaultAsync(gs => gs.GameId == gameId && gs.UserId == user.Id);
        
        if (latestSave == null)
            return TypedResults.NotFound();

        var fileName = latestSave.GetUploadPath();
        
        if (!File.Exists(fileName))
            return TypedResults.NotFound();

        var downloadName = $"{latestSave.Game.Title} - {user.UserName} - {latestSave.CreatedOn}".SanitizeFilename();
        
        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{downloadName}.lcs");
    }

    public static async Task<IResult> DownloadSaveByIdAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] ILogger<Program> logger,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);
            
            return TypedResults.Unauthorized();
        }
        
        var save = await saveService
            .Include(s => s.Game)
            .Include(s => s.StorageLocation)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id);

        var fileName = save.GetUploadPath();
        
        if (!File.Exists(fileName))
            return TypedResults.NotFound();
        
        var downloadName = $"{save.Game.Title} - {user.UserName} - {save.CreatedOn}".SanitizeFilename();
        
        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{downloadName}.lcs");
    }

    public static async Task<IResult> UploadSaveByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        HttpContext httpContext,
        [FromServices] ILogger<Program> logger,
        [FromServices] SettingsProvider<Settings.Settings> settingsProvider,
        [FromServices] SdkMapper sdkMapper,
        [FromServices] UserService userService,
        [FromServices] GameService gameService,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
        {
            logger.LogError("Could not find user from claim principal: {UserName}", userPrincipal?.Identity?.Name);
            
            return TypedResults.Unauthorized();
        }
        
        var game = await gameService.GetAsync(gameId);
        
        if (game == null)
            return TypedResults.NotFound();

        var saveStorageLocation =
            await storageLocationService.FirstOrDefaultAsync(l => l.Default && l.Type == StorageLocationType.Save);

        if (saveStorageLocation == null)
            return TypedResults.InternalServerError(
                "There is no save location available on the server. Check your server settings!");

        var form = await httpContext.Request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();
        
        if (file == null)
            return TypedResults.BadRequest("No file was provided in the request.");
        
        using (var stream = file.OpenReadStream())
        {
            var limits = await userService.GetLimitsAsync(user);

            if (!limits.SavesEnabled)
                return TypedResults.BadRequest("Cloud saves are disabled for your account.");

            if (!limits.StorageUnlimited)
            {
                if (stream.Length > limits.StorageQuotaBytes)
                    return TypedResults.BadRequest("Save exceeds your storage quota.");

                // Cull oldest saves (across all games) to make room for the incoming save.
                var existingSaves = await saveService
                    .Query(q => q
                        .Where(s => s.UserId == user.Id)
                        .OrderBy(s => s.CreatedOn))
                    .GetAsync();

                var used = existingSaves.Sum(s => s.Size);

                foreach (var saveToCull in existingSaves)
                {
                    if (used + stream.Length <= limits.StorageQuotaBytes)
                        break;

                    await saveService.DeleteAsync(saveToCull);
                    used -= saveToCull.Size;
                }
            }

            var save = new GameSave
            {
                Game = game,
                User = user,
                Size = stream.Length,
                StorageLocation = saveStorageLocation,
            };

            stream.Seek(0, SeekOrigin.Begin);

            save = await saveService.AddAsync(save);

            try
            {
                var saveUploadFile = save.GetUploadPath();
                var saveUploadPath = Path.GetDirectoryName(saveUploadFile);

                if (!Directory.Exists(saveUploadPath))
                    Directory.CreateDirectory(saveUploadPath);
                
                using (var fs = File.Create(saveUploadFile))
                {
                    await stream.CopyToAsync(fs);
                }

                if (settingsProvider.CurrentValue.Server.UserSaves.MaxSaves > 0)
                {
                    var savesToCull = await saveService
                        .Query(q =>
                        {
                            return q
                                .Where(s => s.UserId == user.Id && s.GameId == game.Id)
                                .OrderByDescending(s => s.CreatedOn)
                                .Skip(settingsProvider.CurrentValue.Server.UserSaves.MaxSaves);
                        })
                        .GetAsync();

                    foreach (var saveToCull in savesToCull)
                        await saveService.DeleteAsync(saveToCull);
                }

                return TypedResults.Ok(sdkMapper.ToSdk(save));
            }
            catch (Exception ex)
            {
                return TypedResults.InternalServerError(ex);
            }
        }
    }
}