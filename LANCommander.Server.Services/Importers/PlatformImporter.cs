using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PlatformImporter<TParentRecord>(
    PlatformService platformService,
    ImportContext<TParentRecord> importContext) : IImporter<Platform, Data.Models.Platform>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Platform record)
    {
        return new ImportItemInfo
        {
            Name = record.Name,
        };
    }

    public bool CanImport(Platform record) => importContext.Record is Data.Models.Game;

    public async Task<Data.Models.Platform> AddAsync(Platform record)
    {
        try
        {
            var platform = new Data.Models.Platform
            {
                Games = new List<Data.Models.Game>() { importContext.Record as Data.Models.Game },
                Name = record.Name,
            };

            platform = await platformService.AddAsync(platform);

            return platform;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occured while importing platform", ex);
        }
    }

    public async Task<Data.Models.Platform> UpdateAsync(Platform record)
    {
        var existing = await platformService.Include(p => p.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(importContext.Record as Data.Models.Game);
            
            existing = await platformService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occured while importing platform", ex);
        }
    }

    public async Task<bool> ExistsAsync(Platform record)
    {
        return await platformService.ExistsAsync(c => c.Name == record.Name);
    }
}