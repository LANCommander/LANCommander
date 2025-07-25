using LANCommander.SDK.Models.Manifest;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class RedistributableImporter(
    IMapper mapper,
    RedistributableService redistributableService,
    UserService userService) : BaseImporter<Redistributable, Data.Models.Redistributable>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Redistributable record)
    {
        return new ImportItemInfo()
        {
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Redistributable record)
    {
        return new ExportItemInfo()
        {
            Id = record.Id,
            Name = record.Name,
        };
    }

    public override bool CanImport(Redistributable record) => true;
    public override bool CanExport(Redistributable record) => true;

    public override async Task<Data.Models.Redistributable> AddAsync(Redistributable record)
    {
        var redistributable = mapper.Map<Data.Models.Redistributable>(record);

        try
        {
            return await redistributableService.AddAsync(redistributable);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Redistributable>(record,
                "An unknown error occurred while trying to add redistributable", ex);
        }
    }

    public override async Task<Data.Models.Redistributable> UpdateAsync(Redistributable record)
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

            existing = await redistributableService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Redistributable>(record,
                "An unknown error occurred while trying to add redistributable", ex);
        }
    }

    public override async Task<Redistributable> ExportAsync(Guid id)
    {
        return await redistributableService.GetAsync<Redistributable>(id);
    }

    public override async Task<bool> ExistsAsync(Redistributable record)
    {
        return await redistributableService.ExistsAsync(r => r.Id == record.Id || r.Name == record.Name);
    }
} 