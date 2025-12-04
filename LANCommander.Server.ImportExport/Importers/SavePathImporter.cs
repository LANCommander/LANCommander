using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.ImportExport.Importers;

public class SavePathImporter(
    ILogger<SavePathImporter> logger,
    SavePathService savePathService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<SavePath>
{
    public override string GetKey(SavePath record)
        => $"{nameof(SavePath)}/{record.Id}";

    public override async Task<ImportItemInfo<SavePath>> GetImportInfoAsync(SavePath record) 
        => new()
        {
            Type = ImportExportRecordType.SavePath,
            Name = record.Path,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(SavePath record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(SavePath record)
    {
        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            var savePath = new Data.Models.SavePath
            {
                Id = record.Id,
                Path = record.Path,
                WorkingDirectory = record.WorkingDirectory,
                IsRegex = record.IsRegex,
                Type = record.Type,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Game = await gameService.GetAsync(game.Id),
            };

            await savePathService.AddAsync(savePath);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not add save path | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(SavePath record)
    {
        var existing = await savePathService.FirstOrDefaultAsync(p => p.Id == record.Id);

        try
        {
            var game = ImportContext.Manifest as Game;
            
            existing.Path = record.Path;
            existing.WorkingDirectory = record.WorkingDirectory;
            existing.IsRegex = record.IsRegex;
            existing.Type = record.Type;
            existing.Game = await gameService.GetAsync(game.Id);
            
            await savePathService.UpdateAsync(existing);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update save path | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(SavePath record) 
        => await savePathService.ExistsAsync(p => p.Id == record.Id);
}