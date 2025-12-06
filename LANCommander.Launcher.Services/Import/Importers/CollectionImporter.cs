using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class CollectionImporter(
    CollectionService collectionService,
    ILogger<CollectionImporter> logger) : BaseImporter<Collection>
{
    public override async Task<ImportItemInfo<Collection>> GetImportInfoAsync(Collection record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Collection),
            Record = record,
        };

    public override string GetKey(Collection record) => $"{nameof(Collection)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Collection record)
    {
        var existing = await collectionService.FirstOrDefaultAsync(c => c.Name == record.Name);

        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Collection> importItemInfo)
    {
        var collection = new Data.Models.Collection
        {
            Name = importItemInfo.Record.Name,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            await collectionService.AddAsync(collection);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add collection | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Collection> importItemInfo) => true;
    public override async Task<bool> ExistsAsync(ImportItemInfo<Collection> importItemInfo)
        => await collectionService.ExistsAsync(c => c.Name == importItemInfo.Record.Name);
}