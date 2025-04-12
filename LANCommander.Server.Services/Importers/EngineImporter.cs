using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class EngineImporter(
    IMapper mapper,
    EngineService engineService,
    ImportContext importContext) : IImporter<Engine, Data.Models.Engine>
{
    public async Task<ImportItemInfo> GetImportInfoAsync(Engine record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Engine,
            Name = record.Name,
        };
    }

    public async Task<ImportItemInfo> GetExportInfoAsync(Engine record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Engine,
            Name = record.Name,
        };
    }

    public bool CanImport(Engine record) => importContext.DataRecord is Data.Models.Game;
    public bool CanExport(Engine record) => importContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.Engine> AddAsync(Engine record)
    {
        try
        {
            var engine = new Data.Models.Engine
            {
                Games = new List<Data.Models.Game>() { importContext.DataRecord as Data.Models.Game },
                Name = record.Name,
            };

            engine = await engineService.AddAsync(engine);

            return engine;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Engine>(record, "An unknown error occured while importing engine", ex);
        }
    }

    public async Task<Data.Models.Engine> UpdateAsync(Engine record)
    {
        var existing = await engineService.Include(g => g.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(importContext.DataRecord as Data.Models.Game);
            
            existing = await engineService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Engine>(record, "An unknown error occured while importing engine", ex);
        }
    }

    public async Task<Engine> ExportAsync(Data.Models.Engine entity)
    {
        return mapper.Map<Engine>(entity);
    }

    public async Task<bool> ExistsAsync(Engine record)
    {
        return await engineService.ExistsAsync(c => c.Name == record.Name);
    }
}