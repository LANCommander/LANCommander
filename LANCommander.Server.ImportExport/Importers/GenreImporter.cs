using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class GenreImporter(
    IMapper mapper,
    GenreService genreService,
    GameService gameService) : BaseImporter<Genre, Data.Models.Genre>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Genre record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.Genre,
            Name = record.Name,
        };
    }

    public override bool CanImport(Genre record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Genre> AddAsync(Genre record)
    {
        try
        {
            var genre = new Data.Models.Genre
            {
                Games = new List<Data.Models.Game>() { ImportContext.DataRecord as Data.Models.Game },
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

    public override async Task<Data.Models.Genre> UpdateAsync(Genre record)
    {
        var existing = await genreService.Include(g => g.Games).FirstOrDefaultAsync(c => c.Name == record.Name);
        var game = ImportContext.DataRecord as Data.Models.Game;
        
        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();

            if (!existing.Games.Any(g => g.Id == game.Id))
            {
                existing.Games.Add(await gameService.GetAsync(game.Id));
                
                existing = await genreService.UpdateAsync(existing);
            }

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occured while importing genre", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Genre record)
    {
        return await genreService.ExistsAsync(c => c.Name == record.Name);
    }
}