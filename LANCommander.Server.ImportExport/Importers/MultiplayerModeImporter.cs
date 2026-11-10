using System.Linq.Expressions;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

/// <summary>
/// A game can have several modes of the same type, e.g. two LAN modes with different player counts,
/// and modes carry no id. A mode is identified by all of its fields: one that matches an existing
/// mode exactly is that mode, anything else is a new one.
/// </summary>
public class MultiplayerModeImporter(
    ILogger<MultiplayerModeImporter> logger,
    MultiplayerModeService multiplayerModeService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<MultiplayerMode>
{
    protected override bool BelongsToPackageRecord => true;

    public override string GetKey(MultiplayerMode record)
        => $"{nameof(MultiplayerMode)}/{record.Type}:{record.NetworkProtocol}:{record.Description}:{record.MinPlayers}-{record.MaxPlayers}:{record.Spectators}";

    public override async Task<ImportItemInfo<MultiplayerMode>> GetImportInfoAsync(MultiplayerMode record)
        => new()
        {
            Type = ImportExportRecordType.MultiplayerMode,
            Name = String.IsNullOrWhiteSpace(record.Description) ? record.Type.ToString() : $"{record.Type} - {record.Description}",
            Record = record,
        };

    public override async Task<bool> CanImportAsync(MultiplayerMode record) => ImportContext.Manifest is Game;

    private static Expression<Func<Data.Models.MultiplayerMode, bool>> Matches(Guid gameId, MultiplayerMode record)
    {
        var description = record.Description ?? String.Empty;

        return m => m.GameId == gameId
                    && m.Type == record.Type
                    && m.NetworkProtocol == record.NetworkProtocol
                    && (m.Description ?? String.Empty) == description
                    && m.MinPlayers == record.MinPlayers
                    && m.MaxPlayers == record.MaxPlayers
                    && m.Spectators == record.Spectators;
    }

    public override async Task<bool> AddAsync(MultiplayerMode record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;

            var multiplayerMode = new Data.Models.MultiplayerMode
            {
                Description = record.Description,
                Type = record.Type,
                Spectators = record.Spectators,
                MinPlayers = record.MinPlayers,
                MaxPlayers = record.MaxPlayers,
                NetworkProtocol = record.NetworkProtocol,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Game = await gameService.GetAsync(game.Id),
            };

            await multiplayerModeService.AddAsync(multiplayerMode);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add multiplayer mode | {Key}", GetKey(record));
            return false;
        }
    }

    /// <summary>The mode is already there, field for field; nothing to change.</summary>
    public override async Task<bool> UpdateAsync(MultiplayerMode record)
    {
        var game = ImportContext.Manifest as Game;

        return game != null && !ImportContext.InQueue(game, gameImporter);
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(MultiplayerMode record)
    {
        if (ImportContext.Manifest is Game game)
            return await multiplayerModeService.ExistsAsync(Matches(game.Id, record));

        return false;
    }
}
