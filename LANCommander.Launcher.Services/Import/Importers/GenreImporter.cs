using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GenreImporter(
    GenreService genreService,
    ILogger<GenreImporter> logger) : BaseImporter<Genre, Data.Models.Genre>
{
    public override async Task<ImportItemInfo<Genre>> GetImportInfoAsync(Genre record)
    {
        return new ImportItemInfo<Genre>
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Genre),
            Record = record,
        };
    }
    
    public override string GetKey(Genre record) => $"{nameof(Genre)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Genre record)
    {
        var existing = await genreService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Genre> AddAsync(Genre record)
    {
        var genre = new Data.Models.Genre
        {
            Name = record.Name,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await genreService.AddAsync(genre);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occurred while trying to add genre", ex);
        }
    }

    public override async Task<Data.Models.Genre> UpdateAsync(Genre record)
    {
        var existing = await genreService.FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;
            existing.ImportedOn = DateTime.UtcNow;

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Genre>(record, "An unknown error occurred while trying to update genre", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Genre record) => await genreService.ExistsAsync(c => c.Name == record.Name);
}