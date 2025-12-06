using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models.Manifest;
using LANCommander.SDK.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GameImporter(
    GameService gameService,
    LibraryService libraryService,
    ILogger<GameImporter> logger) : BaseImporter<Game>
{
    public override async Task<ImportItemInfo<Game>> GetImportInfoAsync(Game record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Title,
            Type = nameof(Game),
            Record = record,
        };

    public override string GetKey(Game record) => $"{nameof(Game)}/{record.Id}";

    public override async Task<bool> CanImportAsync(Game record)
    {
        var existing = await gameService.GetAsync(record.Id);
        
        if (existing == null)
            return true;
        
        return
            record.UpdatedOn > existing.ImportedOn
            ||
            record.Actions.Any(a => a.UpdatedOn > existing.ImportedOn || a.CreatedOn > existing.ImportedOn)
            ||
            record.SavePaths.Any(a => a.UpdatedOn > existing.ImportedOn || a.CreatedOn > existing.ImportedOn);
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Game> importItemInfo)
    {
        try
        {
            var game = new Data.Models.Game
            {
                Id = importItemInfo.Record.Id,
                Title = importItemInfo.Record.Title,
                SortTitle = importItemInfo.Record.SortTitle,
                Description = importItemInfo.Record.Description,
                Notes = importItemInfo.Record.Notes,
                ReleasedOn = importItemInfo.Record.ReleasedOn,
                Singleplayer = importItemInfo.Record.Singleplayer,
                Type = importItemInfo.Record.Type,
                IGDBId = importItemInfo.Record.IGDBId,
                CreatedOn = importItemInfo.Record.CreatedOn,
                UpdatedOn = importItemInfo.Record.UpdatedOn,
                ImportedOn = DateTime.UtcNow,
            };
            
            await gameService.AddAsync(game);
            await UpdateRelationships(importItemInfo.Record);
            await libraryService.AddToLibraryAsync(game);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add game | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Game> importItemInfo)
    {
        var existing = await gameService.GetAsync(importItemInfo.Record.Id);

        try
        {
            existing.Title = importItemInfo.Record.Title;
            existing.SortTitle = importItemInfo.Record.SortTitle;
            existing.Description = importItemInfo.Record.Description;
            existing.Notes = importItemInfo.Record.Notes;
            existing.ReleasedOn = importItemInfo.Record.ReleasedOn;
            existing.Singleplayer = importItemInfo.Record.Singleplayer;
            existing.Type = importItemInfo.Record.Type;
            existing.IGDBId = importItemInfo.Record.IGDBId;
            existing.CreatedOn = importItemInfo.Record.CreatedOn;
            existing.ImportedOn = DateTime.UtcNow;
            existing.LatestVersion = importItemInfo.Record.Version;
            
            await gameService.UpdateAsync(existing);
            await UpdateRelationships(importItemInfo.Record);
            
            if (await libraryService.IsInstalledAsync(existing.Id) && existing.LatestVersion == existing.InstalledVersion)
                await ManifestHelper.WriteAsync(importItemInfo.Record, existing.InstallDirectory);

            return true;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(importItemInfo.Record, "An unknown error occurred while trying to update game", ex);
        }
    }

    private async Task UpdateRelationships(Game manifest)
    {
        var game = await gameService.GetAsync(manifest.Id);

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Collections,
            manifest.Collections,
            r => c => c.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Developers,
            manifest.Developers,
            r => d => d.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Genres,
            manifest.Genres,
            r => g => g.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Platforms,
            manifest.Platforms,
            r => p => p.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Publishers,
            manifest.Publishers,
            r => p => p.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Tags,
            manifest.Tags,
            r => t => t.Name == r.Name);
    }

    public override async Task<bool> ExistsAsync(ImportItemInfo<Game> importItemInfo) => await gameService.ExistsAsync(importItemInfo.Record.Id);
}