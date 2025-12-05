using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class PlatformImporter(
    PlatformService platformService,
    ILogger<PlatformImporter> logger) : BaseImporter<Platform, Data.Models.Platform>
{
    public override async Task<ImportItemInfo<Platform>> GetImportInfoAsync(Platform record)
    {
        return new ImportItemInfo<Platform>
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Platform),
            Record = record,
        };
    }
    
    public override string GetKey(Platform record) => $"{nameof(Platform)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Platform record)
    {
        var existing = await platformService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Platform> AddAsync(Platform record)
    {
        var platform = new Data.Models.Platform
        {
            Name = record.Name,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await platformService.AddAsync(platform);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occurred while trying to add platform", ex);
        }
    }

    public override async Task<Data.Models.Platform> UpdateAsync(Platform record)
    {
        var existing = await platformService.FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;
            existing.ImportedOn = DateTime.UtcNow;

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Platform>(record, "An unknown error occurred while trying to update platform", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Platform record) => await platformService.ExistsAsync(c => c.Name == record.Name);
}