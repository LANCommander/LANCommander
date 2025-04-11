using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PlatformImporter(
    IMapper mapper,
    PlatformService platformService,
    ImportContext importContext,
    ExportContext exportContext) : IImporter<Platform, Data.Models.Platform>
{
    public async Task<ImportItemInfo> InfoAsync(Platform record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Platforms,
            Name = record.Name,
        };
    }

    public bool CanImport(Platform record) => importContext.DataRecord is Data.Models.Game;
    public bool CanExport(Platform record) => exportContext.DataRecord is Data.Models.Game;

    public async Task<Data.Models.Platform> AddAsync(Platform record)
    {
        try
        {
            var platform = new Data.Models.Platform
            {
                Games = new List<Data.Models.Game>() { importContext.DataRecord as Data.Models.Game },
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
            
            existing.Games.Add(importContext.DataRecord as Data.Models.Game);
            
            existing = await platformService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occured while importing platform", ex);
        }
    }

    public async Task<Platform> ExportAsync(Data.Models.Platform entity)
    {
        return mapper.Map<Platform>(entity);
    }

    public async Task<bool> ExistsAsync(Platform record)
    {
        return await platformService.ExistsAsync(c => c.Name == record.Name);
    }
}