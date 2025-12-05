using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class ScriptImporter(
    ILogger<ScriptImporter> logger,
    ScriptService scriptService,
    GameService gameService,
    RedistributableService redistributableService,
    ServerService serverService,
    GameImporter gameImporter,
    RedistributableImporter redistributableImporter,
    ServerImporter serverImporter) : BaseImporter<Script>
{
    public override string GetKey(Script record)
        => $"{nameof(Script)}/{record.Id}";

    public override async Task<ImportItemInfo<Script>> GetImportInfoAsync(Script record)
        => new()
        {
            Type = ImportExportRecordType.Script,
            Name = record.Name,
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}")?.Size ?? 0,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Script record) =>
        ImportContext.Manifest is Game
        ||
        ImportContext.Manifest is Redistributable
        ||
        ImportContext.Manifest is SDK.Models.Manifest.Server;

    public override async Task<bool> AddAsync(Script record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");
        
        if (archiveEntry != null)
            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = record.Id,
                Name = record.Name,
                Path = archiveEntry.Key!,
            });

        Data.Models.Script script = null;
        string path = "";

        try
        {
            var newScript = new Data.Models.Script
            {
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Name = record.Name,
                Description = record.Description,
                RequiresAdmin = record.RequiresAdmin,
                Type = record.Type,
            };

            if (ImportContext.Manifest is Game game)
            {
                if (ImportContext.InQueue(game, gameImporter))
                    return false;
                
                newScript.Game = await gameService.GetAsync(game.Id);
            }
            else if (ImportContext.Manifest is Redistributable redistributable)
            {
                if (ImportContext.InQueue(redistributable, redistributableImporter))
                    return false;
                
                newScript.Redistributable = await redistributableService.GetAsync(redistributable.Id);
            }
            else if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            {
                if (ImportContext.InQueue(server, serverImporter))
                    return false;
                
                newScript.Server = await serverService.GetAsync(server.Id);
            }
            else
                return false;

            await scriptService.AddAsync(newScript);

            return true;
        }
        catch (Exception ex)
        {
            if (script != null)
                await scriptService.DeleteAsync(script);
            
            logger.LogError(ex, "Failed to add script | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Script record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Scripts/{record.Id}");

        Data.Models.Script existing = await scriptService.FirstOrDefaultAsync(s => s.Id == record.Id);

        if (existing == null)
            return false;

        if (archiveEntry != null)
            AddAsset(new ImportAssetArchiveEntry
            {
                RecordId = record.Id,
                Name = record.Name,
                Path = archiveEntry.Key!,
            });

        try
        {
            if (ImportContext.Manifest is Game game)
            {
                if (ImportContext.InQueue(game, gameImporter))
                    return false;
            
                existing.Game = await gameService.GetAsync(game.Id);
            }
            else if (ImportContext.Manifest is Redistributable redistributable)
            {
                if (ImportContext.InQueue(redistributable, redistributableImporter))
                    return false;
                
                existing.Redistributable = await redistributableService.GetAsync(redistributable.Id);
            }
            else if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            {
                if (ImportContext.InQueue(server, serverImporter))
                    return false;
                
                existing.Server = await serverService.GetAsync(server.Id);
            }
            else
                return false;
            
            existing.CreatedOn = record.CreatedOn;
            existing.Name = record.Name;
            existing.Description = record.Description;
            existing.RequiresAdmin = record.RequiresAdmin;
            existing.Type = record.Type;

            await scriptService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update script | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> IngestAsync(IImportAsset asset)
    {
        var script = await scriptService.GetAsync(asset.RecordId);
        
        if (asset is ImportAssetArchiveEntry assetArchiveEntry)
        {
            var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == assetArchiveEntry.Path);
            
            using (var streamReader = new StreamReader(archiveEntry.OpenEntryStream()))
            {
                script.Contents = await streamReader.ReadToEndAsync();
            }

            await scriptService.UpdateAsync(script);

            return true;
        }
        
        if (asset is ImportAssetText assetText)
        {
            script.Contents = assetText.Contents;
            
            await scriptService.UpdateAsync(script);

            return true;
        }

        return false;
    }

    public override async Task<bool> ExistsAsync(Script record)
        => await scriptService.ExistsAsync(record.Id);
}