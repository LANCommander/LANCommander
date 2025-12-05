using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Importers;

public class ActionImporter(
    ILogger<ActionImporter> logger,
    ActionService actionService,
    GameService gameService,
    ServerService serverService,
    GameImporter gameImporter,
    ServerImporter serverImporter) : BaseImporter<Action>
{
    public override string GetKey(Action record)
        => $"{nameof(Action)}/{record.Name}";

    public override async Task<ImportItemInfo<Action>> GetImportInfoAsync(Action record)
        => new()
        {
            Name = record.Name,
            Type = ImportExportRecordType.Action,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Action record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(Action record)
    {
        try
        {
            var action = new Data.Models.Action
            {
                Name = record.Name,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                PrimaryAction = record.IsPrimaryAction,
                SortOrder = record.SortOrder,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            if (ImportContext.Manifest is Game game && !ImportContext.InQueue(game, gameImporter))
                action.Game = await gameService.GetAsync(game.Id);
            else if (ImportContext.Manifest is SDK.Models.Manifest.Server server &&
                     !ImportContext.InQueue(server, serverImporter))
                action.Server = await serverService.GetAsync(server.Id);
            else
                return false;

            await actionService.AddAsync(action);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add action | {Key}", GetKey(record));
            
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Action record)
    {
        Data.Models.Action existing;
        
        if (ImportContext.Manifest is Game game)
            existing = await actionService.FirstOrDefaultAsync(a => a.Name == record.Name && a.GameId == game.Id);
        else if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            existing = await actionService.FirstOrDefaultAsync(a => a.Name == record.Name && a.ServerId == server.Id);
        else
            return false;

        try
        {
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.PrimaryAction = record.IsPrimaryAction;
            existing.SortOrder = record.SortOrder;
            existing.CreatedOn = record.CreatedOn;
            existing.UpdatedOn = record.UpdatedOn;
            
            await actionService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update action | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(Action record)
    {
        if (ImportContext.Manifest is Game game)
            return await actionService.ExistsAsync(a => a.Name == record.Name && a.GameId == game.Id);

        return false;
    }
}