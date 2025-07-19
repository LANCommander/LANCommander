using AutoMapper;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Models.Manifest;
using LANCommander.Server.Data.Models;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives;
using SharpCompress.Common;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implements importing for save records
/// </summary>
/// <param name="serviceProvider">Valid service provider for injecting the services we need</param>
/// <param name="ImportContext">The context (archive, parent record> of the import</param>
public class SaveImporter(
    IMapper mapper,
    UserService userService,
    GameSaveService gameSaveService) : BaseImporter<Save, Data.Models.GameSave>
{
    public override async Task<ImportItemInfo> GetImportInfoAsync(Save record)
    {
        return new ImportItemInfo
        {
            Flag = ImportRecordFlags.Saves,
            Name = $"{record.User} - {record.CreatedOn}",
            Size = ImportContext.Archive.Entries.FirstOrDefault(e => e.Key == $"Saves/{record.Id}")?.Size ?? 0,
        };
    }

    public override async Task<ExportItemInfo> GetExportInfoAsync(Save record)
    {
        var savePath = await gameSaveService.GetSavePathAsync(record.Id);
        var fileInfo = new FileInfo(savePath);
        
        return new ExportItemInfo
        {
            Flag = ImportRecordFlags.Saves,
            Name = $"{record.User} - {record.CreatedOn}",
            Size = fileInfo.Length,
        };
    }

    public override bool CanImport(Save record) => ImportContext.DataRecord is Data.Models.Game;
    public override bool CanExport(Save record) => ImportContext.DataRecord is Data.Models.Game;

    public override async Task<Data.Models.GameSave> AddAsync(Save record)
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
            save = await gameSaveService.AddAsync(new Data.Models.GameSave()
            {
                CreatedBy = user,
                User = user,
                CreatedOn = record.CreatedOn,
                Game = ImportContext.DataRecord as Data.Models.Game,
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

    public override async Task<Data.Models.GameSave> UpdateAsync(Save record)
    {
        var existing = await gameSaveService.FirstOrDefaultAsync(s => s.User.UserName == record.User && s.CreatedOn == record.CreatedOn);
        
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

            return existing;
        }
        catch (Exception ex)
        {
            throw new ImportSkippedException<Save>(record, "An unknown error occured while importing save file", ex);
        }
    }

    public override async Task<Save> ExportAsync(GameSave entity)
    {
        var path = await gameSaveService.GetSavePathAsync(entity.Id);
        var fileInfo = new FileInfo(path);

        using (var fs = fileInfo.OpenRead())
        {
            ImportContext.Archive.AddEntry($"Saves/{entity.Id}", fs);
        }
        
        return mapper.Map<Save>(entity);
    }

    public override async Task<bool> ExistsAsync(Save archive)
    {
        return await gameSaveService
            .Include(s => s.User)
            .ExistsAsync(s => s.User.UserName == archive.User && s.CreatedOn == archive.CreatedOn && s.GameId == ImportContext.DataRecord.Id);
    }
}