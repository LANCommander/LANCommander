using LANCommander.SDK.Models.Manifest;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implements importing for save records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="importContext">The context (archive, parent record> of the import</param>
public class SaveImporter<TParentRecord>(
    UserService userService,
    GameSaveService gameSaveService,
    ImportContext<TParentRecord> importContext) : IImporter<Save, Data.Models.GameSave>
    where TParentRecord : Data.Models.BaseModel
{
    public async Task<ImportItemInfo> InfoAsync(Save record)
    {
        return new ImportItemInfo
        {
            Name = $"{record.User} - {record.CreatedOn}",
            Size = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}")?.Size ?? 0,
        };
    }

    public bool CanImport(Save record) => importContext.Record is Data.Models.Game;

    public async Task<Data.Models.GameSave> AddAsync(Save record)
    {
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}");

        if (archiveEntry == null)
            throw new ImportSkippedException<Save>(record, "Matching save file does not exist in archive");

        var user = await userService.GetAsync(record.User);

        if (user == null)
            throw new ImportSkippedException<Save>(record, "Save file's owner does not exist");

        Data.Models.GameSave save = null;
        string path = "";

        try
        {
            save = await gameSaveService.AddAsync(new Data.Models.GameSave()
            {
                CreatedBy = user,
                User = user,
                CreatedOn = record.CreatedOn,
                Game = importContext.Record as Data.Models.Game,
                StorageLocation = await gameSaveService.GetDefaultStorageLocationAsync(),
            });

            path = gameSaveService.GetSavePath(save);
            
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
                await gameSaveService.DeleteAsync(save);
            
            throw new ImportSkippedException<Save>(record, "An unknown error occured while importing save file", ex);
        }
    }

    public async Task<Data.Models.GameSave> UpdateAsync(Save record)
    {
        var existing = await gameSaveService.FirstOrDefaultAsync(s => s.User.UserName == record.User && s.CreatedOn == record.CreatedOn);
        
        // We only need to extract the save file
        var archiveEntry = importContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}");

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

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Save>(record, "An unknown error occured while importing save file", ex);
        }
    }

    public async Task<bool> ExistsAsync(Save archive)
    {
        return await gameSaveService
            .Include(s => s.User)
            .ExistsAsync(s => s.User.UserName == archive.User && s.CreatedOn == archive.CreatedOn && s.GameId == importContext.Record.Id);
    }
}