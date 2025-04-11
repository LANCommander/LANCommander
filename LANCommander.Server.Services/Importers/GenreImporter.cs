using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class GenreImporter(
    GenreService genreService,
    ImportContext importContext) : IImporter<Genre, Data.Models.Genre>
{
    public async Task<ImportItemInfo> InfoAsync(Genre record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Genres,
            Name = record.Name,
        };
    }

    public bool CanImport(Genre record) => importContext.DataRecord is Data.Models.Game;
    
    public async Task<Data.Models.Genre> AddAsync(Genre record)
    {
        try
        {
            var genre = new Data.Models.Genre
            {
                Games = new List<Data.Models.Game>() { importContext.DataRecord as Data.Models.Game },
                Name = record.Name,
            };

            genre = await genreService.AddAsync(genre);

            return genre;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occured while importing genre", ex);
        }
    }

    public async Task<Data.Models.Genre> UpdateAsync(Genre record)
    {
        var existing = await genreService.Include(g => g.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(importContext.DataRecord as Data.Models.Game);
            
            existing = await genreService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occured while importing genre", ex);
        }
    }

    public async Task<bool> ExistsAsync(Genre record)
    {
        return await genreService.ExistsAsync(c => c.Name == record.Name);
    }
}