using AutoMapper;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class GameImporter(
    IMapper mapper,
    GameService gameService,
    UserService userService) : BaseImporter<Game, Data.Models.Game>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Game record)
    {
        return new ImportItemInfo
        {
            Name = record.Title,
        };
    }

    public override bool CanImport(Game record) => true;

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
            DirectoryName = record.DirectoryName,
        };
        
        if (!String.IsNullOrWhiteSpace(record.CreatedBy))
            game.CreatedBy = await userService.GetAsync(record.CreatedBy);
        
        if (!String.IsNullOrWhiteSpace(record.UpdatedBy))
            game.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

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

            existing = await gameService.UpdateAsync(existing);
            
            // importContext.UseRecord(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Game record)
    {
        return await gameService.ExistsAsync(g => g.Id == record.Id || g.Title == record.Title);
    }
}
