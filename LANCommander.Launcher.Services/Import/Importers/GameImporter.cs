using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GameImporter(
    GameService gameService,
    CollectionService collectionService,
    CompanyService companyService,
    EngineService engineService,
    GenreService genreService,
    MultiplayerModeService multiplayerModeService,
    PlatformService platformService,
    TagService tagService,
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
            ImportedOn = DateTime.UtcNow,
        };

        var collections = record.Collections.Select(c => c.Name);
        var developers = record.Developers.Select(c => c.Name);
        var engine = record.Engine?.Name;
        var genres = record.Genres.Select(c => c.Name);
        var platforms = record.Platforms.Select(c => c.Name);
        var publishers = record.Publishers.Select(c => c.Name);
        var tags = record.Tags.Select(c => c.Name);
        
        game.Collections = await collectionService.Query(c => collections.Contains(c.Name)).ToListAsync();
        game.Developers = await companyService.Query(c => developers.Contains(c.Name)).ToListAsync();
        game.Engine = await engineService.FirstOrDefaultAsync(e => e.Name == engine);
        game.Genres = await genreService.Query(c => genres.Contains(c.Name)).ToListAsync();
        game.Platforms = await platformService.Query(c => platforms.Contains(c.Name)).ToListAsync();
        game.Publishers = await companyService.Query(c => publishers.Contains(c.Name)).ToListAsync();
        game.Tags = await tagService.Query(c => tags.Contains(c.Name)).ToListAsync();

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
            
            var collections = record.Collections.Select(c => c.Name);
            var developers = record.Developers.Select(c => c.Name);
            var engine = record.Engine.Name;
            var genres = record.Genres.Select(c => c.Name);
            var platforms = record.Platforms.Select(c => c.Name);
            var publishers = record.Publishers.Select(c => c.Name);
            var tags = record.Tags.Select(c => c.Name);
            
            existing.Collections = await collectionService.Query(c => collections.Contains(c.Name)).ToListAsync();
            existing.Developers = await companyService.Query(c => developers.Contains(c.Name)).ToListAsync();
            existing.Engine = await engineService.FirstOrDefaultAsync(e => e.Name == engine);
            existing.Genres = await genreService.Query(c => genres.Contains(c.Name)).ToListAsync();
            existing.Platforms = await platformService.Query(c => platforms.Contains(c.Name)).ToListAsync();
            existing.Publishers = await companyService.Query(c => publishers.Contains(c.Name)).ToListAsync();
            existing.Tags = await tagService.Query(c => tags.Contains(c.Name)).ToListAsync();

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Game>(record, "An unknown error occurred while trying to update game", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Game record) => await gameService.ExistsAsync(record.Id);
}