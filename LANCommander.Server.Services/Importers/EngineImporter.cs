using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Game = LANCommander.Server.Data.Models.Game;

namespace LANCommander.Server.Services.Importers;

public class EngineImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Engine, Data.Models.Engine>
{
    EngineService _engineService = serviceProvider.GetRequiredService<EngineService>();
    
    public async Task<Data.Models.Engine> AddAsync(Engine record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Engine>(record, $"Cannot import engine for a {typeof(TParentRecord).Name}");

        try
        {
            var engine = new Data.Models.Engine
            {
                Games = new List<Game>() { game },
                Name = record.Name,
            };

            engine = await _engineService.AddAsync(engine);

            return engine;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Engine>(record, "An unknown error occured while importing engine", ex);
        }
    }

    public async Task<Data.Models.Engine> UpdateAsync(Engine record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Engine>(record, $"Cannot import engines for a {typeof(TParentRecord).Name}");

        var existing = await _engineService.Include(g => g.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Game>();
            
            existing.Games.Add(game);
            
            existing = await _engineService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Engine>(record, "An unknown error occured while importing engine", ex);
        }
    }

    public async Task<bool> ExistsAsync(Engine record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Engine>(record, $"Cannot import engines for a {typeof(TParentRecord).Name}");
        
        return await _engineService.ExistsAsync(c => c.Name == record.Name);
    }
}