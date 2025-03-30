using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Enums;

namespace LANCommander.Server.Services;

public class ImportService<T>(
    ILogger<ImportService<T>> logger,
    ImportContextFactory<T> contextFactory,
    StorageLocationService storageLocationService,
    ArchiveService archiveService) : BaseService(logger)
    where T : class, IBaseModel
{
    public async Task<T> ImportFromUploadArchiveAsync(Guid objectKey)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

        T entity;
        
        using (var importZip = ZipFile.OpenRead(importArchivePath))
        {
            entity = await importer.ImportAsync(objectKey, importZip);
        }
        
        await archiveService.DeleteAsync(importArchive);

        return entity;
    }

    public async Task<T> ImportFromLocalFileAsync(string localFilePath)
    {
        Guid objectKey = Guid.NewGuid();

        var storageLocation =
            await storageLocationService.FirstOrDefaultAsync(l => l.Default && l.Type == StorageLocationType.Archive);

        var importArchive = await archiveService.AddAsync(new Archive
        {
            ObjectKey = objectKey.ToString(),
            Version = DateTime.UtcNow.ToString(),
            StorageLocation = storageLocation,
        });

        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

        File.Copy(localFilePath, importArchivePath, true);
        
        return await ImportFromUploadArchiveAsync(objectKey);
    }

    public async Task ImportGameAsync(SDK.Models.Game record, Guid objectKey, ImportGameOptions importGameOptions)
    {
        using (var context = contextFactory.Create())
        {
            Data.Models.Game game;
            
            if (await context.Games.ExistsAsync(record))
                game = await context.Games.UpdateAsync(record);
            else
                game = await context.Games.AddAsync(record);

            if (importGameOptions.HasFlag(ImportGameOptions.Actions))
                await context.AddToQueueAsync(record.Actions);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Archives))
                await context.AddToQueueAsync(record.Archives);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Collections))
                await context.AddToQueueAsync(record.Collections);
            
            if (importGameOptions.HasFlag(ImportGameOptions.CustomFields))
                await context.AddToQueueAsync(record.CustomFields);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Developers))
                await context.AddToQueueAsync(record.Developers);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Engine))
                await context.AddToQueueAsync(record.Engine);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Genres))
                await context.AddToQueueAsync(record.Genres);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Keys))
                await context.AddToQueueAsync(record.Keys);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Media))
                await context.AddToQueueAsync(record.Media);
            
            if (importGameOptions.HasFlag(ImportGameOptions.MultiplayerModes))
                await context.AddToQueueAsync(record.MultiplayerModes);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Platforms))
                await context.AddToQueueAsync(record.Platforms);

            if (importGameOptions.HasFlag(ImportGameOptions.Publishers))
                await context.AddToQueueAsync(record.Publishers);

            if (importGameOptions.HasFlag(ImportGameOptions.Saves))
                await context.AddToQueueAsync(record.Saves);

            if (importGameOptions.HasFlag(ImportGameOptions.SavePaths))
                await context.AddToQueueAsync(record.SavePaths);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Scripts))
                await context.AddToQueueAsync(record.Scripts);
            
            if (importGameOptions.HasFlag(ImportGameOptions.Tags))
                await context.AddToQueueAsync(record.Tags);
        }
    }
}