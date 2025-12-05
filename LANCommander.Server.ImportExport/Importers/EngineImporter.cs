using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class EngineImporter(
    ILogger<EngineImporter> logger,
    EngineService engineService) : BaseImporter<Engine>
{
    public override string GetKey(Engine record)
        => $"{nameof(Engine)}/{record.Name}";

    public override async Task<ImportItemInfo<Engine>> GetImportInfoAsync(Engine record)
        => new()
        {
            Type = ImportExportRecordType.Engine,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Engine record) 
        => !await engineService.ExistsAsync(e => e.Name == record.Name);

    public override async Task<bool> AddAsync(Engine record)
    {
        try
        {
            var engine = new Data.Models.Engine
            {
                Name = record.Name,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await engineService.AddAsync(engine);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add engine | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Engine record) => true;
    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(Engine record) 
        => await engineService.ExistsAsync(c => c.Name == record.Name);
}