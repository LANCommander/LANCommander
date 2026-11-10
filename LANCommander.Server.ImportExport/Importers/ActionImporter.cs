using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;
using Action = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Server.ImportExport.Importers;

/// <summary>
/// Imports the actions of a game, server or tool. An action is identified by its name and platforms
/// together, so a game's Windows "Play" and Linux "Play" are two actions; re-importing updates the
/// matching one instead of adding another.
/// </summary>
public class ActionImporter(
    ILogger<ActionImporter> logger,
    ActionService actionService,
    GameService gameService,
    ServerService serverService,
    ToolService toolService,
    GameImporter gameImporter,
    ServerImporter serverImporter,
    ToolImporter toolImporter) : BaseImporter<Action>
{
    protected override bool BelongsToPackageRecord => true;

    public override string GetKey(Action record)
        => $"{nameof(Action)}/{record.Name}:{record.Platforms}";

    public override async Task<ImportItemInfo<Action>> GetImportInfoAsync(Action record)
        => new()
        {
            Name = record.Platforms == RuntimePlatform.None ? record.Name : $"{record.Name} ({record.Platforms})",
            Type = ImportExportRecordType.Action,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Action record)
        => ImportContext.Manifest is Game or SDK.Models.Manifest.Server or Tool;

    /// <summary>
    /// The action's owner once it has been imported, or null while the owner is still waiting in the
    /// queue (or the package can't have actions).
    /// </summary>
    private Guid? ImportedOwnerId => ImportContext.Manifest switch
    {
        Game game when !ImportContext.InQueue(game, gameImporter) => game.Id,
        SDK.Models.Manifest.Server server when !ImportContext.InQueue(server, serverImporter) => server.Id,
        Tool tool when !ImportContext.InQueue(tool, toolImporter) => tool.Id,
        _ => null,
    };

    /// <summary>The existing action this record stands for, on the package's game, server or tool.</summary>
    private Expression<Func<Data.Models.Action, bool>>? Matches(Action record)
    {
        var name = record.Name;
        var platforms = record.Platforms;

        return ImportContext.Manifest switch
        {
            Game game => a => a.GameId == game.Id && a.Name == name && a.Platforms == platforms,
            SDK.Models.Manifest.Server server => a => a.ServerId == server.Id && a.Name == name && a.Platforms == platforms,
            Tool tool => a => a.ToolId == tool.Id && a.Name == name && a.Platforms == platforms,
            _ => null,
        };
    }

    public override async Task<bool> AddAsync(Action record)
    {
        try
        {
            if (ImportedOwnerId is not { } ownerId)
                return false;

            var action = new Data.Models.Action
            {
                Name = record.Name,
                Arguments = record.Arguments,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                Type = record.Type,
                Platforms = record.Platforms,
                SortOrder = record.SortOrder,
                OptionOverrides = record.OptionOverrides,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
            };

            switch (ImportContext.Manifest)
            {
                case Game:
                    action.Game = await gameService.GetAsync(ownerId);
                    break;

                case SDK.Models.Manifest.Server:
                    action.Server = await serverService.GetAsync(ownerId);
                    break;

                case Tool:
                    action.Tool = await toolService.GetAsync(ownerId);
                    break;
            }

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
        if (ImportedOwnerId == null || Matches(record) is not { } matches)
            return false;

        var existing = await actionService.FirstOrDefaultAsync(matches);

        if (existing == null)
            return false;

        try
        {
            existing.Arguments = record.Arguments;
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.Type = record.Type;
            existing.Platforms = record.Platforms;
            existing.SortOrder = record.SortOrder;
            existing.OptionOverrides = record.OptionOverrides;
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
        => Matches(record) is { } matches && await actionService.ExistsAsync(matches);
}
