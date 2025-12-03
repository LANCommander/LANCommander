using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class RedistributableImporter(
    ILogger<RedistributableImporter> logger,
    IMapper mapper,
    RedistributableService redistributableService,
    UserService userService) : BaseImporter<Redistributable>
{
    public override string GetKey(Redistributable record)
        => $"{nameof(Redistributable)}/{record.Id}";

    public override async Task<ImportItemInfo<Redistributable>> GetImportInfoAsync(Redistributable record) 
        => new()
        {
            Type = ImportExportRecordType.Redistributable,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Redistributable record) => true;

    public override async Task<bool> AddAsync(Redistributable record)
    {
        var redistributable = mapper.Map<Data.Models.Redistributable>(record);

        try
        {
            await redistributableService.AddAsync(redistributable);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add redistributable | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Redistributable record)
    {
        var existing = await redistributableService.FirstOrDefaultAsync(r => r.Id == record.Id || r.Name == record.Name);

        try
        {
            existing.Name = record.Name;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.CreatedOn = record.CreatedOn;
            existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
            existing.UpdatedOn = record.UpdatedOn;
            existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

            await redistributableService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update redistributable | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(Redistributable record) 
        => await redistributableService.ExistsAsync(r => r.Id == record.Id || r.Name == record.Name);
} 