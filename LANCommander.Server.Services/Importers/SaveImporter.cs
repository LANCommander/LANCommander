using LANCommander.SDK.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implements importing for save records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="importContext">The context (archive, parent record> of the import</param>
public class SaveImporter<TParentRecord>(ServiceProvider serviceProvider, ImportContext<TParentRecord> importContext) : IImporter<GameSave, Data.Models.GameSave>
{
    UserService _userService = serviceProvider.GetRequiredService<UserService>();
    GameSaveService _gameSaveService = serviceProvider.GetRequiredService<GameSaveService>();
    
    public async Task<Data.Models.GameSave> AddAsync(GameSave record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<GameSave>(record, "Matching save file does not exist in archive");

        var user = await _userService.GetAsync(record.User.UserName);

        if (user == null)
            throw new ImportSkippedException<GameSave>(record, "Save file's owner does not exist");

        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<GameSave>(record,
                $"Cannot import a save for a {typeof(TParentRecord).Name}");

        Data.Models.GameSave save = null;
        string path = "";

        try
        {
            save = await _gameSaveService.AddAsync(new Data.Models.GameSave()
            {
                CreatedBy = user,
                User = user,
                CreatedOn = record.CreatedOn,
                Game = game,
                StorageLocation = await _gameSaveService.GetDefaultStorageLocationAsync(),
            });

            path = _gameSaveService.GetSavePath(save);
            
            archiveEntry.WriteToFile(path, new ExtractionOptions()
            {
                Overwrite = true,
                PreserveAttributes = true,
                PreserveFileTime = true,
            });

            return save;
        }
        catch (Exception ex)
        {
            if (save != null)
                await _gameSaveService.DeleteAsync(save);
            
            throw new ImportSkippedException<GameSave>(record, "An unknown error occured while importing save file", ex);
        }
    }

    public async Task<Data.Models.GameSave> UpdateAsync(GameSave record)
    {
        var existing = await _gameSaveService.FirstOrDefaultAsync(s => s.User.UserName == record.User.UserName && s.CreatedOn == record.CreatedOn);
        
        // We only need to extract the save file
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<GameSave>(record, "Matching save file does not exist in archive");
        
        try
        {
            var path = _gameSaveService.GetSavePath(existing);
            
            archiveEntry.WriteToFile(path, new ExtractionOptions()
            {
                Overwrite = true,
                PreserveAttributes = true,
                PreserveFileTime = true,
            });

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<GameSave>(record, "An unknown error occured while importing save file", ex);
        }
    }

    public async Task<bool> ExistsAsync(GameSave archive)
    {
        if (importContext.Record is not Data.Models.Game game)
            throw new ImportSkippedException<GameSave>(archive,
                $"Cannot import a save for a {typeof(TParentRecord).Name}");
        
        return await _gameSaveService
            .Include(s => s.User)
            .ExistsAsync(s => s.User.UserName == archive.User.UserName && s.CreatedOn == archive.CreatedOn && s.GameId == game.Id);
    }
}