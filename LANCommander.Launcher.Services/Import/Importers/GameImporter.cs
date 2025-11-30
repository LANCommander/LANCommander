using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GameImporter(
    GameService gameService,
    ILogger<GameImporter> logger) : BaseImporter<Game, Data.Models.Game>
{
    public override async Task<ImportItemInfo<Game>> GetImportInfoAsync(Game record)
    {
        return new ImportItemInfo<Game>
        {
            Key = GetKey(record),
            Name = record.Title,
            Type = nameof(Game),
            Record = record,
        };
    }
    
    public override string GetKey(Game record) => $"{nameof(Game)}/{record.Id}";

    public override async Task<bool> CanImportAsync(Game record)
    {
        var existing = await gameService.GetAsync(record.Id);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Game> AddAsync(Game record)
    {
        var game = new Data.Models.Game
        {
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
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await gameService.AddAsync(game);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to add game", ex);
        }
    }

    public override async Task<Data.Models.Game> UpdateAsync(Game record)
    {
        var existing = await gameService.GetAsync(record.Id);

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
            existing.ImportedOn = DateTime.UtcNow;

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Game record) => await gameService.ExistsAsync(record.Id);
}