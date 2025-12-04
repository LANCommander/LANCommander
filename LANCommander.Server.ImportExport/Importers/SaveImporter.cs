using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;
using LANCommander.Server.Services;
using Microsoft.Extensions.Logging;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.ImportExport.Importers;

/// <summary>
/// Implements importing for save records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="ImportContext">The context (archive, parent record> of the import</param>
public class SaveImporter(
    ILogger<SaveImporter> logger,
    UserService userService,
    GameSaveService gameSaveService,
    GameService gameService,
    GameImporter gameImporter) : BaseImporter<Save>
{
    public override string GetKey(Save record)
        => $"{nameof(Save)}/{record.Id}";

    public override async Task<ImportItemInfo<Save>> GetImportInfoAsync(Save record) 
        => new()
        {
            Type = ImportExportRecordType.Save,
            Name = $"{record.User} - {record.CreatedOn}",
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}")?.Size ?? 0,
            Record = record,
        };

    public override async Task<bool> CanImportAsync(Save record) => ImportContext.Manifest is Game;

    public override async Task<bool> AddAsync(Save record)
    {
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<Save>(record, "Matching save file does not exist in archive");

        var user = await userService.GetAsync(record.User);

        if (user == null)
            throw new ImportSkippedException<Save>(record, "Save file's owner does not exist");

        Data.Models.GameSave save = null;
        string path = "";

        try
        {
            var game = ImportContext.Manifest as Game;

            if (game == null)
                return false;

            if (ImportContext.InQueue(game, gameImporter))
                return false;
            
            save = await gameSaveService.AddAsync(new Data.Models.GameSave
            {
                CreatedBy = user,
                User = user,
                CreatedOn = record.CreatedOn,
                UpdatedOn = record.UpdatedOn,
                Game = await gameService.GetAsync(game.Id),
                StorageLocation = await gameSaveService.GetDefaultStorageLocationAsync(),
            });

            path = gameSaveService.GetSavePath(save);
            
            archiveEntry.WriteToFile(path, new ExtractionOptions()
            {
                Overwrite = true,
                PreserveAttributes = true,
                PreserveFileTime = true,
            });

            return true;
        }
        catch (Exception ex)
        {
            if (save != null)
                await gameSaveService.DeleteAsync(save);
            
            logger.LogError(ex, "An error occured while adding save | {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> UpdateAsync(Save record)
    {
        var existing = await gameSaveService.FirstOrDefaultAsync(s => s.Id == record.Id);
        
        // We only need to extract the save file
        var archiveEntry = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<Save>(record, "Matching save file does not exist in archive");
        
        try
        {
            var path = gameSaveService.GetSavePath(existing);
            
            archiveEntry.WriteToFile(path, new ExtractionOptions()
            {
                Overwrite = true,
                PreserveAttributes = true,
                PreserveFileTime = true,
            });

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not update save file {Key}", GetKey(record));
            return false;
        }
    }

    public override async Task<bool> ExistsAsync(Save save)
        => await gameSaveService.ExistsAsync(save.Id);
}