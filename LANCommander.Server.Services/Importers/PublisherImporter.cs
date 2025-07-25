using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PublisherImporter(
    IMapper mapper,
    CompanyService companyService) : BaseImporter<Company, Data.Models.Company>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Publishers,
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Company record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ImportRecordFlags.Publishers,
            Name = record.Name,
        };
    }

    public override bool CanImport(Company record) => ImportContext.DataRecord is Data.Models.Company;
    public override bool CanExport(Company record) => ImportContext.DataRecord is Data.Models.Company;

    public override async Task<Data.Models.Company> AddAsync(Company record)
    {
        try
        {
            var company = new Data.Models.Company
            {
                PublishedGames = new List<Data.Models.Game>() { ImportContext.DataRecord as Data.Models.Game },
                Name = record.Name,
            };

            company = await companyService.AddAsync(company);

            return company;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing publisher", ex);
        }
    }

    public override async Task<Data.Models.Company> UpdateAsync(Company record)
    {
        var existing = await companyService.Include(g => g.PublishedGames).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.PublishedGames == null)
                existing.PublishedGames = new List<Data.Models.Game>();
            
            existing.PublishedGames.Add(ImportContext.DataRecord as Data.Models.Game);
            
            existing = await companyService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing publisher", ex);
        }
    }

    public override async Task<Company> ExportAsync(Guid id)
    {
        return await companyService.GetAsync<Company>(id);
    }

    public override async Task<bool> ExistsAsync(Company record)
    {
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}