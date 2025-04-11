using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class PublisherImporter(
    IMapper mapper,
    CompanyService companyService,
    ImportContext importContext,
    ExportContext exportContext) : IImporter<Company, Data.Models.Company>
{
    public async Task<ImportItemInfo> InfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Publishers,
            Name = record.Name,
        };
    }

    public bool CanImport(Company record) => importContext.DataRecord is Data.Models.Company;
    public bool CanExport(Company record) => exportContext.DataRecord is Data.Models.Company;

    public async Task<Data.Models.Company> AddAsync(Company record)
    {
        try
        {
            var company = new Data.Models.Company
            {
                PublishedGames = new List<Data.Models.Game>() { importContext.DataRecord as Data.Models.Game },
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

    public async Task<Data.Models.Company> UpdateAsync(Company record)
    {
        var existing = await companyService.Include(g => g.PublishedGames).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.PublishedGames == null)
                existing.PublishedGames = new List<Data.Models.Game>();
            
            existing.PublishedGames.Add(importContext.DataRecord as Data.Models.Game);
            
            existing = await companyService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing publisher", ex);
        }
    }

    public async Task<Company> ExportAsync(Data.Models.Company entity)
    {
        return mapper.Map<Company>(entity);
    }

    public async Task<bool> ExistsAsync(Company record)
    {
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}