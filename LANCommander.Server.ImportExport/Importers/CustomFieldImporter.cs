using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class CustomFieldImporter(
    ILogger<CustomFieldImporter> logger,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<GameCustomField>
{
    public override string GetKey(GameCustomField record)
        => $"{nameof(GameCustomField)}/{record.Name}";

    public override async Task<ImportItemInfo<GameCustomField>> GetImportInfoAsync(GameCustomField record) 
        => new()
        {
            Type = ImportExportRecordType.CustomField,
            Name = record.Name,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(GameCustomField record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(GameCustomField record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            await gameService.SetCustomFieldAsync(game.Id, record.Name, record.Value);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add custom field | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(GameCustomField record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;
            
            var existing = await gameService.GetCustomFieldAsync(game.Id, record.Name);
            
            if (existing.Value != record.Value)
                await gameService.SetCustomFieldAsync(game.Id, record.Name, record.Value);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update custom field | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(GameCustomField record)
    {
        if (ImportContext.Manifest is Game game)
        {
            var customField = await gameService.GetCustomFieldAsync(game.Id, record.Name);

            return customField != null;
        }

        return false;
    }
}