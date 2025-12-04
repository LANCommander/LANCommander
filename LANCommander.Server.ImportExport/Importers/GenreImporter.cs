using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class GenreImporter(
    ILogger<GenreImporter> logger,
    GenreService genreService) : BaseImporter<Genre>
{
    public override string GetKey(Genre record)
        => $"{nameof(Genre)}/{record.Name}";

    public override async Task<ImportItemInfo<Genre>> GetImportInfoAsync(Genre record)
    {
        return new ImportItemInfo<Genre>
        {
            Type = ImportExportRecordType.Genre,
            Name = record.Name,
            Record = record,
        };
    }

    public override async Task<bool> CanImportAsync(Genre record)
        => !await genreService.ExistsAsync(g => g.Name == record.Name);

    public override async Task<bool> AddAsync(Genre record)
    {
        try
        {
            var genre = new Data.Models.Genre
            {
                Name = record.Name,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await genreService.AddAsync(genre);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add genre | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Genre record) => true;

    public override async Task<bool> ExistsAsync(Genre record) 
        => await genreService.ExistsAsync(c => c.Name == record.Name);
}