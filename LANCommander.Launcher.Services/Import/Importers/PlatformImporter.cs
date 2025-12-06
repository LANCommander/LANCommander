using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class PlatformImporter(
    PlatformService platformService,
    ILogger<PlatformImporter> logger) : BaseImporter<Platform>
{
    public override async Task<ImportItemInfo<Platform>> GetImportInfoAsync(Platform record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Platform),
            Record = record,
        };

    public override string GetKey(Platform record) => $"{nameof(Platform)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Platform record)
    {
        var existing = await platformService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Platform> importItemInfo)
    {
        var platform = new Data.Models.Platform
        {
            Name = importItemInfo.Record.Name,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            await platformService.AddAsync(platform);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add platform | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Platform> importItemInfo) => true;
    public override async Task<bool> ExistsAsync(ImportItemInfo<Platform> importItemInfo)
        => await platformService.ExistsAsync(c => c.Name == importItemInfo.Record.Name);
}