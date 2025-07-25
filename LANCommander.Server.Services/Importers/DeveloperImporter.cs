using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class DeveloperImporter(
    IMapper mapper,
    CompanyService companyService) : BaseImporter<Company, Data.Models.Company>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Developers,
            Name = record.Name,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Data.Models.Company record)
    {
        return new ExportItemInfo
        {
            Id = record.Id,
            Flag = ImportRecordFlags.Developers,
            Name = record.Name,
        };
    }

    public override bool CanImport(Company record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Company record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Company> AddAsync(Company record)
    {
        if (ImportContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {ImportContext.DataRecord.GetType().Name}");

        try
        {
            var company = new Data.Models.Company
            {
                DevelopedGames = new List<Data.Models.Game>() { game },
                Name = record.Name,
            };

            company = await companyService.AddAsync(company);

            return company;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing developer", ex);
        }
    }

    public override async Task<Data.Models.Company> UpdateAsync(Company record)
    {
        if (ImportContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {ImportContext.DataRecord.GetType().Name}");

        var existing = await companyService.Include(g => g.DevelopedGames).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.DevelopedGames == null)
                existing.DevelopedGames = new List<Data.Models.Game>();
            
            existing.DevelopedGames.Add(game);
            
            existing = await companyService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing developer", ex);
        }
    }

    public override async Task<Company> ExportAsync(Guid id)
    {
        return await companyService.GetAsync<Company>(id);
    }

    public override async Task<bool> ExistsAsync(Company record)
    {
        if (ImportContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {ImportContext.DataRecord.GetType().Name}");
        
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}