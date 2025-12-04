using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class CollectionImporter(
    ILogger<CollectionImporter> logger,
    CollectionService collectionService) : BaseImporter<Collection>
{
    public override string GetKey(Collection record)
        => $"{nameof(Collection)}/{record.Name}";

    public override async Task<ImportItemInfo<Collection>> GetImportInfoAsync(Collection record)
        => new()
        {
            Type = ImportExportRecordType.Collection,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Collection record)
        => !await collectionService.ExistsAsync(c => c.Name == record.Name);

    public override async Task<bool> AddAsync(Collection record)
    {
        try
        {
            var collection = new Data.Models.Collection
            {
                Name = record.Name,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await collectionService.AddAsync(collection);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add collection | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Collection record) => true;

    public override async Task<bool> ExistsAsync(Collection record)
        => await collectionService.ExistsAsync(c => c.Name == record.Name);
}