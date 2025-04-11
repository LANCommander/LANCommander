using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CollectionImporter(
    IMapper mapper,
    CollectionService collectionService,
    ImportContext importContext,
    ExportContext exportContext) : IImporter<Collection, Data.Models.Collection>
{
    public async Task<ImportItemInfo> InfoAsync(Collection record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Collections,
            Name = record.Name,
        };
    }

    public bool CanImport(Collection record) => importContext.DataRecord is Data.Models.Game;
    public bool CanExport(Collection record) => exportContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.Collection> AddAsync(Collection record)
    {
        try
        {
            var collection = new Data.Models.Collection
            {
                Games = new List<Data.Models.Game>() { importContext.DataRecord as Data.Models.Game },
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
            
            existing.Games.Add(importContext.DataRecord as Data.Models.Game);
            
            existing = await collectionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public async Task<Collection> ExportAsync(Data.Models.Collection entity)
    {
        return mapper.Map<Collection>(entity);
    }

    public async Task<bool> ExistsAsync(Collection record)
    {
        return await collectionService.ExistsAsync(c => c.Name == record.Name);
    }
}