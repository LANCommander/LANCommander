using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CollectionImporter<TParentRecord>(
    CollectionService collectionService,
    ImportContext<TParentRecord> importContext) : IImporter<Collection, Data.Models.Collection>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Collection record)
    {
        return new ImportItemInfo
        {
            Name = record.Name,
        };
    }

    public bool CanImport(Collection record) => importContext.Record is Data.Models.Game;
    
    public async Task<Data.Models.Collection> AddAsync(Collection record)
    {
        try
        {
            var collection = new Data.Models.Collection
            {
                Games = new List<Data.Models.Game>() { importContext.Record as Data.Models.Game },
                Name = record.Name,
            };

            collection = await collectionService.AddAsync(collection);

            return collection;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public async Task<Data.Models.Collection> UpdateAsync(Collection record)
    {
        var existing = await collectionService.Include(c => c.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(importContext.Record as Data.Models.Game);
            
            existing = await collectionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public async Task<bool> ExistsAsync(Collection record)
    {
        return await collectionService.ExistsAsync(c => c.Name == record.Name);
    }
}