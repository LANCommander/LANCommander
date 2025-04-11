using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class DeveloperImporter(
    CompanyService companyService,
    ImportContext importContext) : IImporter<Company, Data.Models.Company>
{
    public async Task<ImportItemInfo> InfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Developers,
            Name = record.Name,
        };
    }

    public bool CanImport(Company record) => importContext.DataRecord is Data.Models.Game;
    
    public async Task<Data.Models.Company> AddAsync(Company record)
    {
        if (importContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {importContext.DataRecord.GetType().Name}");

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

    public async Task<Data.Models.Company> UpdateAsync(Company record)
    {
        if (importContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {importContext.DataRecord.GetType().Name}");

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

    public async Task<bool> ExistsAsync(Company record)
    {
        if (importContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {importContext.DataRecord.GetType().Name}");
        
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}