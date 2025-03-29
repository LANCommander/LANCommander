using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Services.Importers;

public class CustomFieldImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<GameCustomField>
{
    GameService _gameService = serviceProvider.GetRequiredService<GameService>();
    
    public async Task<GameCustomField> AddAsync(GameCustomField record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<GameCustomField>(record, $"Cannot import customField for a {typeof(TParentRecord).Name}");

        try
        {
            await _gameService.SetCustomFieldAsync(game.Id, record.Name, record.Value);

            return record;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public async Task<GameCustomField> UpdateAsync(GameCustomField record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<GameCustomField>(record, $"Cannot import customFields for a {typeof(TParentRecord).Name}");

        var existing = await _gameService.GetCustomFieldAsync(game.Id, record.Name);

        try
        {
            if (existing.Value != record.Value)
                await _gameService.SetCustomFieldAsync(game.Id, record.Name, record.Value);

            return record;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameCustomField>(record, "An unknown error occured while importing customField", ex);
        }
    }

    public async Task<bool> ExistsAsync(GameCustomField record)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<GameCustomField>(record, $"Cannot import custom fields for a {typeof(TParentRecord).Name}");

        return (await _gameService.GetCustomFieldAsync(game.Id, record.Name)) == null;
    }
}