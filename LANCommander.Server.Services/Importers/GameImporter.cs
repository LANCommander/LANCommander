using AutoMapper;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class GameImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Game, Data.Models.Game>
{
    private readonly GameService _gameService = serviceProvider.GetService<GameService>();
    private readonly UserService _userService = serviceProvider.GetService<UserService>();
    private readonly IMapper _mapper = serviceProvider.GetService<IMapper>();
    
    public async Task<Data.Models.Game> AddAsync(Game record)
    {
        var game = _mapper.Map<Data.Models.Game>(record);

        try
        {
            return await _gameService.AddAsync(game);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to add game", ex);
        }
    }

    public async Task<Data.Models.Game> UpdateAsync(Game record)
    {
        var existing = await _gameService.FirstOrDefaultAsync(g => g.Id == record.Id || g.Title == record.Title);

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
            existing.CreatedBy = await _userService.GetAsync(record.CreatedBy);
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedBy = await _userService.GetAsync(record.UpdatedBy);
            existing.DirectoryName = record.DirectoryName;

            existing = await _gameService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

    public async Task<bool> ExistsAsync(Game record)
    {
        return await _gameService.ExistsAsync(g => g.Id == record.Id || g.Title == record.Title);
    }
}