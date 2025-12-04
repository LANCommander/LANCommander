using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class PlatformImporter(
    ILogger<PlatformImporter> logger,
    PlatformService platformService) : BaseImporter<Platform>
{
    public override string GetKey(Platform record)
        => $"{nameof(Platform)}/{record.Name}";

    public override async Task<ImportItemInfo<Platform>> GetImportInfoAsync(Platform record) 
        => new()
        {
            Type = ImportExportRecordType.Platform,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Platform record) 
        => !await platformService.ExistsAsync(p => p.Name == record.Name);

    public override async Task<bool> AddAsync(Platform record)
    {
        try
        {
            var platform = new Data.Models.Platform
            {
                Name = record.Name,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await platformService.AddAsync(platform);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add platform | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Platform record) => true;

    public override async Task<bool> ExistsAsync(Platform record)
        => await platformService.ExistsAsync(c => c.Name == record.Name);
}