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
        
        if (archiveEntry == null)
            throw new ImportSkippedException<Script>(record, "Matching script file does not exist in import archive");

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
                newScript.Game = await gameService.GetAsync(game.Id);
            else if (ImportContext.Manifest is Redistributable redistributable)
                newScript.Redistributable = await redistributableService.GetAsync(redistributable.Id);
            else if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
                newScript.Server = await serverService.GetAsync(server.Id);
            else
                return false;

            using (var streamReader = new StreamReader(archiveEntry.OpenEntryStream()))
            {
                newScript.Contents = await streamReader.ReadToEndAsync();
            }

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

        Data.Models.Script existing = null;
        
        if (ImportContext.Manifest is Game game)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        else if (ImportContext.Manifest is Redistributable redistributable)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        else if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            existing = await scriptService.FirstOrDefaultAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);

        if (existing == null)
            return false;

        try
        {
            existing.CreatedOn = record.CreatedOn;
            existing.Name = record.Name;
            existing.Description = record.Description;
            existing.RequiresAdmin = record.RequiresAdmin;
            existing.Type = record.Type;

            using (var streamReader = new StreamReader(archiveEntry.OpenEntryStream()))
            {
                existing.Contents = await streamReader.ReadToEndAsync();
            }

            await scriptService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update script | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(Script record)
    {
        if (ImportContext.Manifest is Game game)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.GameId == game.Id);
        
        if (ImportContext.Manifest is Redistributable redistributable)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.RedistributableId == redistributable.Id);
        
        if (ImportContext.Manifest is SDK.Models.Manifest.Server server)
            return await scriptService.ExistsAsync(s => s.Type == record.Type && s.Name == record.Name && s.ServerId == server.Id);

        return false;
    }
}