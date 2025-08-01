using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.ImportExport.Importers;

public class DeveloperImporter(
    IMapper mapper,
    CompanyService companyService,
    GameService gameService) : BaseImporter<Company, Data.Models.Company>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Company record)
    {
        return new ImportItemInfo
        {
            Type = ImportExportRecordType.Developer,
            Name = record.Name,
        };
    }

    public override bool CanImport(Company record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.Company> AddAsync(Company record)
    {
        if (ImportContext.DataRecord is not Data.Models.Game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {ImportContext.DataRecord.GetType().Name}");

        try
        {
            var game = ImportContext.DataRecord as Data.Models.Game;
            
            var company = new Data.Models.Company
            {
                DevelopedGames = [await gameService.GetAsync(game.Id)],
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
        if (ImportContext.DataRecord is not Data.Models.Game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {ImportContext.DataRecord.GetType().Name}");

        var existing = await companyService.Include(g => g.DevelopedGames).FirstOrDefaultAsync(c => c.Name == record.Name);

        try
        {
            var game = ImportContext.DataRecord as Data.Models.Game;
            
            if (existing.DevelopedGames == null)
                existing.DevelopedGames = new List<Data.Models.Game>();

            if (!existing.DevelopedGames.Any(g => g.Id == game.Id))
            {
                existing.DevelopedGames.Add(await gameService.GetAsync(game.Id));
            
                existing = await companyService.UpdateAsync(existing);
            }

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Company>(record, "An unknown error occured while importing developer", ex);
        }
    }

    public override async Task<bool> ExistsAsync(Company record)
    {
        if (ImportContext.DataRecord is not Data.Models.Game game)
            throw new ImportSkippedException<Company>(record, $"Cannot import developers for a {ImportContext.DataRecord.GetType().Name}");
        
        return await companyService.ExistsAsync(c => c.Name == record.Name);
    }
}