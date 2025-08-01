using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class EngineImporter(
    IMapper mapper,
    EngineService engineService,
    GameService gameService) : BaseImporter<Engine, Data.Models.Engine>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Engine record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.Engine,
            Name = record.Name,
        };
    }

    public override bool CanImport(Engine record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Engine> AddAsync(Engine record)
    {
        try
        {
            var engine = new Data.Models.Engine
            {
                Games = new List<Data.Models.Game>() { ImportContext.DataRecord as Data.Models.Game },
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

    public override async Task<Data.Models.Engine> UpdateAsync(Engine record)
    {
        var existing = await engineService.Include(g => g.Games).FirstOrDefaultAsync(c => c.Name == record.Name);
        var game = ImportContext.DataRecord as Data.Models.Game;
        
        try
        {
            
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();

            if (!existing.Games.Any(g => g.Id == game.Id))
            {
                existing.Games.Add(await gameService.GetAsync(game.Id));
                
                existing = await engineService.UpdateAsync(existing);
            }

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Engine>(record, "An unknown error occured while importing engine", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Engine record)
    {
        return await engineService.ExistsAsync(c => c.Name == record.Name);
    }
}