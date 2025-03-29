using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Game = LANCommander.Server.Data.Models.Game;

namespace LANCommander.Server.Services.Importers;

public class GenreImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Genre>
{
    GenreService _genreService = serviceProvider.GetRequiredService<GenreService>();
    
    public async Task<Genre> AddAsync(Genre record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Genre>(record, $"Cannot import genres for a {typeof(TParentRecord).Name}");

        try
        {
            var genre = new Data.Models.Genre
            {
                Games = new List<Game>() { game },
                Name = record.Name,
            };

            genre = await _genreService.AddAsync(genre);

            return record;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occured while importing genre", ex);
        }
    }

    public async Task<Genre> UpdateAsync(Genre record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Genre>(record, $"Cannot import genres for a {typeof(TParentRecord).Name}");

        var existing = await _genreService.Include(g => g.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Game>();
            
            existing.Games.Add(game);
            
            existing = await _genreService.UpdateAsync(existing);

            return record;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occured while importing genre", ex);
        }
    }

    public async Task<bool> ExistsAsync(Genre record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Genre>(record, $"Cannot import genres for a {typeof(TParentRecord).Name}");
        
        return await _genreService.ExistsAsync(c => c.Name == record.Name);
    }
}