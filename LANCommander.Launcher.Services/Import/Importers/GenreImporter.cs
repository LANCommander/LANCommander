using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class GenreImporter(
    GenreService genreService,
    ILogger<GenreImporter> logger) : BaseImporter<Genre>
{
    public override async Task<ImportItemInfo<Genre>> GetImportInfoAsync(Genre record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Genre),
            Record = record,
        };

    public override string GetKey(Genre record) => $"{nameof(Genre)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Genre record)
    {
        var existing = await genreService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Genre> importItemInfo)
    {
        var genre = new Data.Models.Genre
        {
            Name = importItemInfo.Record.Name,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            await genreService.AddAsync(genre);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add genre | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Genre> importItemInfo) => true;
    public override async Task<bool> ExistsAsync(ImportItemInfo<Genre> importItemInfo) 
        => await genreService.ExistsAsync(c => c.Name == importItemInfo.Record.Name);
}