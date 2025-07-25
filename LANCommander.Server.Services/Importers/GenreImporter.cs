using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class GenreImporter(
    IMapper mapper,
    GenreService genreService) : BaseImporter<Genre, Data.Models.Genre>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Genre record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Genres,
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Genre record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ImportRecordFlags.Genres,
            Name = record.Name,
        };
    }

    public override bool CanImport(Genre record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Genre record) => ImportContext.DataRecord is Data.Models.Game;

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

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(ImportContext.DataRecord as Data.Models.Game);
            
            existing = await genreService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occured while importing genre", ex);
        }
    }

    public override async Task<Genre> ExportAsync(Guid id)
    {
        return await genreService.GetAsync<Genre>(id);
    }

    public override async Task<bool> ExistsAsync(Genre record)
    {
        return await genreService.ExistsAsync(c => c.Name == record.Name);
    }
}