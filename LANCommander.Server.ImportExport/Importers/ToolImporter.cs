using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class ToolImporter(
    ILogger<ToolImporter> logger,
    IMapper mapper,
    ToolService toolService,
    UserService userService) : BaseImporter<Tool>
{
    public override string GetKey(Tool record)
        => $"{nameof(Tool)}/{record.Id}";

    public override async Task<ImportItemInfo<Tool>> GetImportInfoAsync(Tool record) 
        => new()
        {
            Type = ImportExportRecordType.Tool,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Tool record) => true;

    public override async Task<bool> AddAsync(Tool record)
    {
        var tool = mapper.Map<Data.Models.Tool>(record);

        try
        {
            await toolService.AddAsync(tool);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add tool | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Tool record)
    {
        var existing = await toolService.FirstOrDefaultAsync(r => r.Id == record.Id || r.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.CreatedOn = record.CreatedOn;
            existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
            existing.UpdatedOn = record.UpdatedOn;
            existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

            await toolService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update tool | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(Tool record) 
        => await toolService.ExistsAsync(r => r.Id == record.Id || r.Name == record.Name);
} 