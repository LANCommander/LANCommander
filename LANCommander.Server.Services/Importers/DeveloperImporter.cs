using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class DeveloperImporter<TParentRecord>(
    CompanyService companyService,
    ImportContext<TParentRecord> importContext) : IImporter<Company, Data.Models.Company>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Name = record.Name,
        };
    }

    public bool CanImport(Company record) => importContext.Record is Data.Models.Game;
    
    public async Task<Data.Models.Company> AddAsync(Company record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {typeof(TParentRecord).Name}");

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
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {typeof(TParentRecord).Name}");

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
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {typeof(TParentRecord).Name}");
        
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}