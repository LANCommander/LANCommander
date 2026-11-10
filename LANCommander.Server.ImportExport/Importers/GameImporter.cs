using LANCommander.SDK.Enums;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class GameImporter(
    ILogger<GameImporter> logger,
    GameService gameService,
    GameVersionService gameVersionService,
    RedistributableService redistributableService,
    EngineService engineService,
    UserService userService) : BaseImporter<Game>
{
    public override string GetKey(Game record)
        => $"{nameof(Game)}/{record.Id}";

    public override async Task<ImportItemInfo<Game>> GetImportInfoAsync(Game record)
    {
        return new ImportItemInfo<Game>
        {
            Type = ImportExportRecordType.Game,
            Name = record.Title,
            Record = record,
        };
    }

    public override async Task<bool> CanImportAsync(Game record) => true;

    /// <summary>
    /// Records exported before InstallTo/ShowInLibrary existed only carry a (possibly legacy)
    /// Type; derive the install behavior from it the same way the database migration does.
    /// </summary>
    private static void ApplyInstallBehavior(Data.Models.Game game, Game record)
    {
        var hasBaseGame = record.BaseGameId != Guid.Empty || !String.IsNullOrWhiteSpace(record.BaseGame);
        var resolved = GameTypeHelper.FromLegacyType(record.Type, hasBaseGame);

        game.Type = resolved.Type;
        game.InstallTo = record.InstallTo ?? resolved.InstallTo;
        game.ShowInLibrary = record.ShowInLibrary ?? resolved.ShowInLibrary;
    }

    public override async Task<bool> AddAsync(Game record)
    {
        var game = new Data.Models.Game
        {
            Id = record.Id,
            Title = record.Title,
            SortTitle = record.SortTitle,
            Description = record.Description,
            Notes = record.Notes,
            ReleasedOn = record.ReleasedOn,
            Singleplayer = record.Singleplayer,
            Type = record.Type,
            ExternalIds = record.ExternalIds?.Select(e => new Data.Models.GameExternalId
            {
                Provider = e.Provider,
                ExternalId = e.ExternalId,
            }).ToList(),
            CreatedOn = record.CreatedOn,
            UpdatedOn = record.UpdatedOn,
            DirectoryName = record.DirectoryName,
        };

        ApplyInstallBehavior(game, record);

        if (!String.IsNullOrWhiteSpace(record.CreatedBy))
            game.CreatedBy = await userService.GetAsync(record.CreatedBy);
        
        if (!String.IsNullOrWhiteSpace(record.UpdatedBy))
            game.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

        try
        {
            await gameService.AddAsync(game);
            await UpdateRelationshipsAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add game | {Key}", GetKey(record));
            return false;
        }
    }

    /// <summary>
    /// The game this record is, by id, or else by title so a game moved from another server merges
    /// into the copy already here. Two games can share a title (a remake, a reboot), so a title match
    /// must not contradict the record's release year or its external ids.
    /// </summary>
    private async Task<Data.Models.Game?> FindExistingAsync(Game record)
    {
        var byId = await gameService
            .Include(g => g.ExternalIds)
            .FirstOrDefaultAsync(g => g.Id == record.Id);

        if (byId != null)
            return byId;

        if (String.IsNullOrWhiteSpace(record.Title))
            return null;

        var sameTitle = await gameService
            .Include(g => g.ExternalIds)
            .GetAsync(g => g.Title == record.Title);

        return sameTitle.FirstOrDefault(candidate => IsSameGame(candidate, record));
    }

    private static bool IsSameGame(Data.Models.Game candidate, Game record)
    {
        if (candidate.ReleasedOn.HasValue && candidate.ReleasedOn.Value != default && record.ReleasedOn != default
            && candidate.ReleasedOn.Value.Year != record.ReleasedOn.Year)
            return false;

        // Both know the game by the same provider, under different ids
        foreach (var externalId in record.ExternalIds ?? [])
        {
            var candidateId = candidate.ExternalIds?.FirstOrDefault(e => e.Provider == externalId.Provider);

            if (candidateId != null && candidateId.ExternalId != externalId.ExternalId)
                return false;
        }

        return true;
    }

    public override async Task<bool> UpdateAsync(Game record)
    {
        var existing = await FindExistingAsync(record);

        if (existing == null)
            return false;

        try
        {
            record.Id = existing.Id;
            existing.Title = record.Title;
            existing.SortTitle = record.SortTitle;
            existing.Description = record.Description;
            existing.Notes = record.Notes;
            existing.ReleasedOn = record.ReleasedOn;
            existing.Singleplayer = record.Singleplayer;
            existing.Type = record.Type;
            existing.ExternalIds = record.ExternalIds?.Select(e => new Data.Models.GameExternalId
            {
                Provider = e.Provider,
                ExternalId = e.ExternalId,
            }).ToList();
            existing.CreatedOn = record.CreatedOn;
            existing.DirectoryName = record.DirectoryName;

            ApplyInstallBehavior(existing, record);

            if (!String.IsNullOrWhiteSpace(record.CreatedBy))
                existing.CreatedBy = await userService.GetAsync(record.CreatedBy);
        
            if (!String.IsNullOrWhiteSpace(record.UpdatedBy))
                existing.UpdatedBy = await userService.GetAsync(record.UpdatedBy);

            await gameService.UpdateAsync(existing);
            await UpdateRelationshipsAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update game | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        throw new NotImplementedException();
    }

    public override async Task<bool> ExistsAsync(Game record) => await FindExistingAsync(record) != null;

    public async Task UpdateRelationshipsAsync()
    {
        if (ImportContext.Manifest is not Game)
            return;
        
        var manifest = ImportContext.Manifest as Game;

        if (manifest == null)
            return;

        var game = await gameService
            .GetAsync(manifest.Id);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Collections,
            manifest.Collections,
            r => c => c.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Developers,
            manifest.Developers,
            r => c => c.Name == r.Name);

        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Genres,
            manifest.Genres,
            r => g => g.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Platforms,
            manifest.Platforms,
            r => p => p.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Publishers,
            manifest.Publishers,
            r => p => p.Name == r.Name);
        
        await gameService.SyncRelatedCollectionAsync(
            game,
            g => g.Tags,
            manifest.Tags,
            r => t => t.Name == r.Name);

        // The engine record is imported (or found) by name before the game; nothing linked it
        if (!String.IsNullOrWhiteSpace(manifest.Engine?.Name))
        {
            var engine = await engineService.FirstOrDefaultAsync(e => e.Name == manifest.Engine.Name);

            if (engine != null && game.EngineId != engine.Id)
            {
                game.EngineId = engine.Id;
                game.Engine = engine;

                await gameService.UpdateAsync(game);
            }
        }

        if (manifest.Redistributables != null && manifest.Redistributables.Any())
        {
            await gameService.SyncRelatedCollectionAsync(
                game,
                g => g.Redistributables,
                manifest.Redistributables,
                r => rd => rd.Id == r.Id || rd.Name == r.Name);

            // Redistributables and their options belong to the latest version; the game's own list mirrors it
            var versionId = await gameVersionService.GetOrCreateLatestIdAsync(game.Id);
            var options = new Dictionary<Guid, string>();

            foreach (var redistributable in manifest.Redistributables)
            {
                var existing = await redistributableService.FirstOrDefaultAsync(r => r.Id == redistributable.Id || r.Name == redistributable.Name);

                if (existing == null)
                    continue;

                options[existing.Id] = redistributable.Options != null && redistributable.Options.Any()
                    ? System.Text.Json.JsonSerializer.Serialize(redistributable.Options)
                    : null;
            }

            await gameVersionService.SetRedistributablesAsync(versionId, options.Keys);

            foreach (var (redistributableId, optionsJson) in options.Where(o => o.Value != null))
                await gameVersionService.SetRedistributableOptionsAsync(versionId, redistributableId, optionsJson);
        }

        // The option schema was dropped on import before it belonged to versions
        if (!string.IsNullOrWhiteSpace(manifest.OptionSchema))
            await gameVersionService.SetOptionSchemaAsync(await gameVersionService.GetOrCreateLatestIdAsync(game.Id), manifest.OptionSchema);

        if (!string.IsNullOrWhiteSpace(manifest.BaseGame) || manifest.BaseGameId != Guid.Empty)
        {
            Data.Models.Game? baseGame = null;

            if (manifest.BaseGameId != Guid.Empty)
                baseGame = await gameService.FirstOrDefaultAsync(g => g.Id == manifest.BaseGameId);

            if (baseGame == null && !String.IsNullOrWhiteSpace(manifest.BaseGame))
                baseGame = await gameService.FirstOrDefaultAsync(g => g.Title == manifest.BaseGame);

            if (baseGame != null && game.BaseGameId != baseGame.Id)
            {
                game.BaseGameId = baseGame.Id;
                await gameService.UpdateAsync(game);
            }
        }
    }
}
