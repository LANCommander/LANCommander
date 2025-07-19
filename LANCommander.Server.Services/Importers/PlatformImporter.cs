using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PlatformImporter(
    IMapper mapper,
    PlatformService platformService) : BaseImporter<Platform, Data.Models.Platform>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Platform record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Platforms,
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Platform record)
    {
        return new ExportItemInfo
        {
            Flag = ImportRecordFlags.Platforms,
            Name = record.Name,
        };
    }

    public override bool CanImport(Platform record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Platform record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Platform> AddAsync(Platform record)
    {
        try
        {
            var platform = new Data.Models.Platform
            {
                Games = new List<Data.Models.Game>() { ImportContext.DataRecord as Data.Models.Game },
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

    public override async Task<Data.Models.Platform> UpdateAsync(Platform record)
    {
        var existing = await platformService.Include(p => p.Games).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.Games == null)
                existing.Games = new List<Data.Models.Game>();
            
            existing.Games.Add(ImportContext.DataRecord as Data.Models.Game);
            
            existing = await platformService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occured while importing platform", ex);
        }
    }

    public override async Task<Platform> ExportAsync(Data.Models.Platform entity)
    {
        return mapper.Map<Platform>(entity);
    }

    public override async Task<bool> ExistsAsync(Platform record)
    {
        return await platformService.ExistsAsync(c => c.Name == record.Name);
    }
}