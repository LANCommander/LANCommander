using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class PublisherImporter(
    ILogger<PublisherImporter> logger,
    CompanyService companyService) : BaseImporter<Company>
{
    public override string GetKey(Company record)
        => $"Publisher/{record.Name}";

    public override async Task<ImportItemInfo<Company>> GetImportInfoAsync(Company record) 
        => new()
        {
            Type = ImportExportRecordType.Publisher,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Company record)
        => !await companyService.ExistsAsync(c => c.Name == record.Name);

    public override async Task<bool> AddAsync(Company record)
    {
        try
        {
            var company = new Data.Models.Company
            {
                Name = record.Name,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            await companyService.AddAsync(company);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add company | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Company record) => true;

    public override async Task<bool> ExistsAsync(Company record) 
        => await companyService.ExistsAsync(c => c.Name == record.Name);
}