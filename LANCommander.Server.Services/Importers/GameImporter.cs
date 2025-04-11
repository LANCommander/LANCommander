using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class GameImporter(
    IMapper mapper,
    GameService gameService,
    UserService userService,
    ImportContext importContext,
    ExportContext exportContext) : IImporter<Game, Data.Models.Game>
{
    public async Task<ImportItemInfo> InfoAsync(Game record)
    {
        return new ImportItemInfo
        {
            Name = record.Title,
        };
    }

    public bool CanImport(Game record) => true;
    public bool CanExport(Game record) => true;

    public async Task<Data.Models.Game> AddAsync(Game record)
    {
        var game = mapper.Map<Data.Models.Game>(record);

        try
        {
            return await gameService.AddAsync(game);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to add game", ex);
        }
    }

    public async Task<Data.Models.Game> UpdateAsync(Game record)
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
            existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);
            existing.DirectoryName = record.DirectoryName;

            existing = await gameService.UpdateAsync(existing);
            
            // importContext.UseRecord(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

    public async Task<Game> ExportAsync(Data.Models.Game entity)
    {
        return mapper.Map<Game>(entity);
    }

    public async Task<bool> ExistsAsync(Game record)
    {
        return await gameService.ExistsAsync(g => g.Id == record.Id || g.Title == record.Title);
    }
}