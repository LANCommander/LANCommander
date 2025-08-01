using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class PublisherImporter(
    IMapper mapper,
    CompanyService companyService,
    GameService gameService) : BaseImporter<Company, Data.Models.Company>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.Publisher,
            Name = record.Name,
        };
    }

    public override bool CanImport(Company record) => ImportContext.DataRecord is Data.Models.Company;

    public override async Task<Data.Models.Company> AddAsync(Company record)
    {
        try
        {
            var game = ImportContext.DataRecord as Data.Models.Game;
            
            var company = new Data.Models.Company
            {
                PublishedGames = [await gameService.GetAsync(game.Id)],
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
        var game = ImportContext.DataRecord as Data.Models.Game;

        try
        {
            if (existing.PublishedGames == null)
                existing.PublishedGames = new List<Data.Models.Game>();
            
            if (!existing.PublishedGames.Any(g => g.Id == game.Id))
            {
                existing.PublishedGames.Add(await gameService.GetAsync(game.Id));
            
                existing = await companyService.UpdateAsync(existing);
            }

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing publisher", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Company record)
    {
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}