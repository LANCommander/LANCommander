using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class EngineImporter(
    EngineService engineService,
    ILogger<EngineImporter> logger) : BaseImporter<Engine, Data.Models.Engine>
{
    public override async Task<ImportItemInfo<Engine>> GetImportInfoAsync(Engine record)
    {
        return new ImportItemInfo<Engine>
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = nameof(Engine),
            Record = record,
        };
    }
    
    public override string GetKey(Engine record) => $"{nameof(Engine)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Engine record)
    {
        var existing = await engineService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Engine> AddAsync(Engine record)
    {
        var engine = new Data.Models.Engine
        {
            Name = record.Name,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await engineService.AddAsync(engine);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Engine>(record, "An unknown error occurred while trying to add engine", ex);
        }
    }

    public override async Task<Data.Models.Engine> UpdateAsync(Engine record)
    {
        var existing = await engineService.FirstOrDefaultAsync(c => c.Name == record.Name);

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
            throw new ImportSkippedException<Engine>(record, "An unknown error occurred while trying to update engine", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Engine record) => await engineService.ExistsAsync(c => c.Name == record.Name);
}