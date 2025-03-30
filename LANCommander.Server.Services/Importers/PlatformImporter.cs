using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Game = LANCommander.Server.Data.Models.Game;

namespace LANCommander.Server.Services.Importers;

public class PlatformImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Platform, Data.Models.Platform>
{
    PlatformService _platformService = serviceProvider.GetRequiredService<PlatformService>();
    
    public async Task<Data.Models.Platform> AddAsync(Platform record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Platform>(record, $"Cannot import platforms for a {typeof(TParentRecord).Name}");

        try
        {
            var platform = new Data.Models.Platform
            {
                Games = new List<Game>() { game },
                Name = record.Name,
            };

            platform = await _platformService.AddAsync(platform);

            return platform;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occured while importing platform", ex);
        }
    }

    public async Task<Data.Models.Platform> UpdateAsync(Platform record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Platform>(record, $"Cannot import platforms for a {typeof(TParentRecord).Name}");

        var existing = await _platformService.Include(p => p.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Game>();
            
            existing.Games.Add(game);
            
            existing = await _platformService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occured while importing platform", ex);
        }
    }

    public async Task<bool> ExistsAsync(Platform record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Platform>(record, $"Cannot import platforms for a {typeof(TParentRecord).Name}");
        
        return await _platformService.ExistsAsync(c => c.Name == record.Name);
    }
}