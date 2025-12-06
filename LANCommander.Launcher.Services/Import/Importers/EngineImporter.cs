using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class EngineImporter(
    EngineService engineService,
    ILogger<EngineImporter> logger) : BaseImporter<Engine>
{
    public override async Task<ImportItemInfo<Engine>> GetImportInfoAsync(Engine record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Engine),
            Record = record,
        };

    public override string GetKey(Engine record) => $"{nameof(Engine)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Engine record)
    {
        var existing = await engineService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Engine> importItemInfo)
    {
        var engine = new Data.Models.Engine
        {
            Name = importItemInfo.Record.Name,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            await engineService.AddAsync(engine);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add engine | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Engine> importItemInfo) => true;
    public override async Task<bool> ExistsAsync(ImportItemInfo<Engine> importItemInfo) 
        => await engineService.ExistsAsync(c => c.Name == importItemInfo.Record.Name);
}