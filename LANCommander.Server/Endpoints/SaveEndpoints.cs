using System.Security.Claims;
using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Extensions;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Mvc;
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

    public static async Task<IResult> GetAsync([FromServices] GameSaveService saveService)
    {
        var saves = await saveService.GetAsync<SDK.Models.GameSave>();

        return TypedResults.Ok(saves);
    }

    public static async Task<IResult> GetByIdAsync(
        Guid id,
        [FromServices] GameSaveService saveService)
    {
        var save = await saveService.FirstOrDefaultAsync<SDK.Models.GameSave>(s => s.Id == id);
        
        if (save == null)
            return TypedResults.NotFound();
        
        return TypedResults.Ok(save);
    }

    public static async Task<IResult> DeleteByIdAsync(
        Guid id,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
            return TypedResults.Unauthorized();
        
        var save = await saveService.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id);
        
        if (save == null)
            return TypedResults.NotFound();
        
        await saveService.DeleteAsync(save);
        
        return TypedResults.NoContent();
    }
    
    public static async Task<IResult> GetSavesByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
            return TypedResults.Unauthorized();
        
        var userSaves = await saveService.GetAsync<SDK.Models.GameSave>(gs => gs.UserId == user.Id && gs.GameId == gameId);
        
        return TypedResults.Ok(userSaves);
    }

    public static async Task<IResult> GetLatestSaveByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
            return TypedResults.Unauthorized();
        
        var latestSave = await saveService
            .SortBy(s => s.CreatedOn, SortDirection.Descending)
            .FirstOrDefaultAsync<SDK.Models.GameSave>(gs => gs.GameId == gameId);
        
        if (latestSave == null)
            return TypedResults.NotFound();

        return TypedResults.Ok(latestSave);
    }

    public static async Task<IResult> DownloadLatestSaveByGameAsync(
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
            return TypedResults.Unauthorized();
        
        var latestSave = await saveService
            .SortBy(s => s.CreatedOn, SortDirection.Descending)
            .Include(gs => gs.Game)
            .FirstOrDefaultAsync(gs => gs.GameId == gameId);
        
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
        [FromServices] UserService userService,
        [FromServices] GameSaveService saveService)
    {
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
            return TypedResults.Unauthorized();
        
        var save = await saveService
            .Include(s => s.Game)
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id);

        var fileName = save.GetUploadPath();
        
        if (!File.Exists(fileName))
            return TypedResults.NotFound();
        
        var downloadName = $"{save.Game.Title} - {user.UserName} - {save.CreatedOn}".SanitizeFilename();
        
        return TypedResults.File(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read), "application/octet-stream", $"{downloadName}.lcs");
    }

    public static async Task<IResult> UploadSaveByGameAsync(
        IFormFile file,
        Guid gameId,
        ClaimsPrincipal userPrincipal,
        [FromServices] IMapper mapper,
        [FromServices] UserService userService,
        [FromServices] GameService gameService,
        [FromServices] StorageLocationService storageLocationService,
        [FromServices] GameSaveService saveService)
    {
        var settings = SettingService.GetSettings();
        var user = await userService.GetAsync(userPrincipal?.Identity?.Name ?? string.Empty);

        if (user == null)
            return TypedResults.Unauthorized();
        
        var game = await gameService.GetAsync(gameId);
        
        if (game == null)
            return TypedResults.NotFound();

        var saveStorageLocation =
            await storageLocationService.FirstOrDefaultAsync(l => l.Default && l.Type == StorageLocationType.Save);

        if (saveStorageLocation == null)
            return TypedResults.InternalServerError(
                "There is no save location available on the server. Check your server settings!");

        var save = new GameSave
        {
            Game = game,
            User = user,
            Size = file.Length,
            StorageLocation = saveStorageLocation,
        };
        
        save = await saveService.AddAsync(save);

        try
        {
            var saveUploadPath = Path.GetDirectoryName(save.GetUploadPath());

            if (!Directory.Exists(saveStorageLocation.Path))
                Directory.CreateDirectory(saveStorageLocation.Path);

            using (var stream = File.Create(save.GetUploadPath()))
            {
                await file.CopyToAsync(stream);
            }

            if (settings.UserSaves.MaxSaves > 0)
            {
                var savesToCull = await saveService
                    .Query(q =>
                    {
                        return q
                            .Where(s => s.UserId == user.Id && s.GameId == game.Id)
                            .OrderByDescending(s => s.CreatedOn)
                            .Skip(settings.UserSaves.MaxSaves);
                    })
                    .GetAsync();

                foreach (var saveToCull in savesToCull)
                    await saveService.DeleteAsync(saveToCull);
            }

            return TypedResults.Ok(mapper.Map<SDK.Models.GameSave>(save));
        }
        catch (Exception ex)
        {
            return TypedResults.InternalServerError(ex);
        }
    }
}