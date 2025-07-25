using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CollectionImporter(
    IMapper mapper,
    CollectionService collectionService) : BaseImporter<Collection, Data.Models.Collection>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Collection record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Collections,
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Collection record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ImportRecordFlags.Collections,
            Name = record.Name,
        };
    }

    public override bool CanImport(Collection record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Collection record) => ImportContext.DataRecord is Data.Models.Game;

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

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(ImportContext.DataRecord as Data.Models.Game);
            
            existing = await collectionService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Collection>(record, "An unknown error occured while importing collection", ex);
        }
    }

    public override async Task<Collection> ExportAsync(Guid id)
    {
        return await collectionService.GetAsync<Collection>(id);
    }

    public override async Task<bool> ExistsAsync(Collection record)
    {
        return await collectionService.ExistsAsync(c => c.Name == record.Name);
    }
}