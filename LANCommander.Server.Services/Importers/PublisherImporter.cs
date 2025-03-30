using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using Game = LANCommander.Server.Data.Models.Game;

namespace LANCommander.Server.Services.Importers;

public class PublisherImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<Company, Data.Models.Company>
{
    CompanyService _companyService = serviceProvider.GetRequiredService<CompanyService>();
    
    public async Task<Data.Models.Company> AddAsync(Company record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import publishers for a {typeof(TParentRecord).Name}");

        try
        {
            var company = new Data.Models.Company
            {
                PublishedGames = new List<Game>() { game },
                Name = record.Name,
            };

            company = await _companyService.AddAsync(company);

            return company;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing publisher", ex);
        }
    }

    public async Task<Data.Models.Company> UpdateAsync(Company record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import publishers for a {typeof(TParentRecord).Name}");

        var existing = await _companyService.Include(g => g.PublishedGames).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            if (existing.PublishedGames == null)
                existing.PublishedGames = new List<Game>();
            
            existing.PublishedGames.Add(game);
            
            existing = await _companyService.UpdateAsync(existing);

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing publisher", ex);
        }
    }

    public async Task<bool> ExistsAsync(Company record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import publishers for a {typeof(TParentRecord).Name}");
        
        return await _companyService.ExistsAsync(c => c.Name == record.Name);
    }
}