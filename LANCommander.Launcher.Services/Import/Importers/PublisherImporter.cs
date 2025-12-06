using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Import.Importers;

public class PublisherImporter(
    CompanyService companyService,
    ILogger<PublisherImporter> logger) : BaseImporter<Company>
{
    public override async Task<ImportItemInfo<Company>> GetImportInfoAsync(Company record, BaseManifest manifest) =>
        new()
        {
            Key = GetKey(record),
            Name = record.Name,
            Type = "Publisher",
            Record = record,
        };

    public override string GetKey(Company record) => $"{nameof(Company)}/{record.Name}";

    public override async Task<bool> CanImportAsync(Company record)
    {
        var existing = await companyService.FirstOrDefaultAsync(c => c.Name == record.Name);
        
        if (existing == null)
            return true;
        
        return record.UpdatedOn > existing.ImportedOn;
    }

    public override async Task<bool> AddAsync(ImportItemInfo<Company> importItemInfo)
    {
        var company = new Data.Models.Company
        {
            Name = importItemInfo.Record.Name,
            CreatedOn = importItemInfo.Record.CreatedOn,
            UpdatedOn = importItemInfo.Record.UpdatedOn,
            ImportedOn = DateTime.UtcNow,
        };

        try
        {
            await companyService.AddAsync(company);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add publisher | {Key}", GetKey(importItemInfo.Record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(ImportItemInfo<Company> importItemInfo) => true;
    public override async Task<bool> ExistsAsync(ImportItemInfo<Company> importItemInfo)
        => await companyService.ExistsAsync(c => c.Name == importItemInfo.Record.Name);
}