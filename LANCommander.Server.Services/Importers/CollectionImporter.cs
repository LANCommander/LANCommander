using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Game = LANCommander.Server.Data.Models.Game;

namespace LANCommander.Server.Services.Importers;

public class CollectionImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Collection>
{
    CollectionService _collectionService = serviceProvider.GetRequiredService<CollectionService>();
    
    public async Task<Collection> AddAsync(Collection record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Collection>(record, $"Cannot import collections for a {typeof(TParentRecord).Name}");

        try
        {
            var collection = new Data.Models.Collection
            {
                Games = new List<Game>() { game },
                Name = record.Name,
            };

            collection = await _collectionService.AddAsync(collection);

            return record;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public async Task<Collection> UpdateAsync(Collection record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Collection>(record, $"Cannot import collections for a {typeof(TParentRecord).Name}");

        var existing = await _collectionService.Include(c => c.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Game>();
            
            existing.Games.Add(game);
            
            existing = await _collectionService.UpdateAsync(existing);

            return record;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public async Task<bool> ExistsAsync(Collection record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Collection>(record, $"Cannot import collections for a {typeof(TParentRecord).Name}");
        
        return await _collectionService.ExistsAsync(c => c.Name == record.Name);
    }
}