using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class CollectionImporter(
    IMapper mapper,
    CollectionService collectionService,
    GameService gameService) : BaseImporter<Collection, Data.Models.Collection>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Collection record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.Collection,
            Name = record.Name,
        };
    }

    public override bool CanImport(Collection record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Collection> AddAsync(Collection record)
    {
        try
        {
            var collection = new Data.Models.Collection
            {
                Games = new List<Data.Models.Game>() { ImportContext.DataRecord as Data.Models.Game },
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

    public override async Task<Data.Models.Collection> UpdateAsync(Collection record)
    {
        var existing = await collectionService.Include(c => c.Games).FirstOrDefaultAsync(c => c.Name == record.Name);
        var game = ImportContext.DataRecord as Data.Models.Game;
        
        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();

            if (!existing.Games.Any(g => g.Id == game.Id))
            {
                existing.Games.Add(await gameService.GetAsync(game.Id));
                
                existing = await collectionService.UpdateAsync(existing);
            }

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Collection record)
    {
        return await collectionService.ExistsAsync(c => c.Name == record.Name);
    }
}