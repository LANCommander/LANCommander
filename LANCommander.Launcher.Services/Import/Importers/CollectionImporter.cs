using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class CollectionImporter(
    CollectionService collectionService,
    ILogger<CollectionImporter> logger) : BaseImporter<Collection, Data.Models.Collection>
{
    public override async Task<ImportItemInfo<Collection>> GetImportInfoAsync(Collection record)
    {
        return new ImportItemInfo<Collection>
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Collection),
            Record = record,
        };
    }
    
    public override string GetKey(Collection record) => $"{nameof(Collection)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Collection record)
    {
        var existing = await collectionService.FirstOrDefaultAsync(c => c.Name == record.Name);

        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Collection> AddAsync(Collection record)
    {
        var collection = new Data.Models.Collection
        {
            Name = record.Name,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await collectionService.AddAsync(collection);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occurred while trying to add collection", ex);
        }
    }

    public override async Task<Data.Models.Collection> UpdateAsync(Collection record)
    {
        var existing = await collectionService.FirstOrDefaultAsync(c => c.Name == record.Name);

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
            throw new ImportSkippedException<Collection>(record, "An unknown error occurred while trying to update collection", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Collection record) => await collectionService.ExistsAsync(c => c.Name == record.Name);
}