using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class DeveloperImporter(
    CompanyService companyService,
    ILogger<DeveloperImporter> logger) : BaseImporter<Company, Data.Models.Company>
{
    public override async Task<ImportItemInfo<Company>> GetImportInfoAsync(Company record)
    {
        return new ImportItemInfo<Company>
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = "Developer",
            Record = record,
        };
    }
    
    public override string GetKey(Company record) => $"{nameof(Company)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Company record)
    {
        var existing = await companyService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<Data.Models.Company> AddAsync(Company record)
    {
        var company = new Data.Models.Company
        {
            Name = record.Name,
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            return await companyService.AddAsync(company);
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occurred while trying to add company", ex);
        }
    }

    public override async Task<Data.Models.Company> UpdateAsync(Company record)
    {
        var existing = await companyService.FirstOrDefaultAsync(c => c.Name == record.Name);

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
            throw new ImportSkippedException<Company>(record, "An unknown error occurred while trying to update company", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Company record) => await companyService.ExistsAsync(c => c.Name == record.Name);
}