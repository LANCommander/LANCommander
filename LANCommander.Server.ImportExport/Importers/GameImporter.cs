using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class GameImporter(
    ILogger<GameImporter> logger,
    GameService gameService,
    UserService userService) : BaseImporter<Game>
{
    public override string GetKey(Game record)
        => $"{nameof(Game)}/{record.Id}";

    public override async Task<ImportItemInfo<Game>> GetImportInfoAsync(Game record)
    {
        return new ImportItemInfo<Game>
        {
            Type = ImportExportRecordType.Game,
            Name = record.Title,
            Record = record,
        };
    }

    public override async Task<bool> CanImportAsync(Game record) => true;

    public override async Task<bool> AddAsync(Game record)
    {
        var game = new Data.Models.Game
        {
            Id = record.Id,
            Title = record.Title,
            SortTitle = record.SortTitle,
            Description = record.Description,
            Notes = record.Notes,
            ReleasedOn = record.ReleasedOn,
            Singleplayer = record.Singleplayer,
            Type = record.Type,
            IGDBId = record.IGDBId,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            DirectoryName = record.DirectoryName,
        };
        
        if (!String.IsNullOrWhiteSpace(record.CreatedBy))
            game.CreatedBy = await userService.GetAsync(record.CreatedBy);
        
        if (!String.IsNullOrWhiteSpace(record.UpdatedBy))
            game.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

        try
        {
            await gameService.AddAsync(game);
            await UpdateRelationshipsAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add game | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Game record)
    {
        var existing = await gameService.FirstOrDefaultAsync(g => g.Id == record.Id || g.Title == record.Title);

        try
        {
            existing.Title = record.Title;
            existing.SortTitle = record.SortTitle;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.ReleasedOn = record.ReleasedOn;
            existing.Singleplayer = record.Singleplayer;
            existing.Type = record.Type;
            existing.IGDBId = record.IGDBId;
            existing.CreatedOn = record.CreatedOn;
            existing.DirectoryName = record.DirectoryName;
            
            if (!String.IsNullOrWhiteSpace(record.CreatedBy))
                existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
        
            if (!String.IsNullOrWhiteSpace(record.UpdatedBy))
                existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

            await gameService.UpdateAsync(existing);
            await UpdateRelationshipsAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update game | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(Game record)
    {
        return await gameService.ExistsAsync(g => g.Id == record.Id || g.Title == record.Title);
    }

    public async Task UpdateRelationshipsAsync()
    {
        if (ImportContext.Manifest is not Game)
            return;
        
        var manifest = ImportContext.Manifest as Game;

        if (manifest == null)
            return;

        var game = await gameService
            .GetAsync(manifest.Id);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Collections,
            manifest.Collections,
            r => c => c.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Developers,
            manifest.Developers,
            r => c => c.Name == r.Name);

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
}
