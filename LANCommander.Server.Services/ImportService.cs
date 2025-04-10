using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.Logging;
using LANCommander.SDK.Enums;
using LANCommander.Server.Services.Enums;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress.Archives.Zip;

namespace LANCommander.Server.Services;

public class ImportService(
    ILogger<ImportService> logger,
    ImportContext<Data.Models.Game> gameContext,
    ImportContext<Data.Models.Redistributable> redistributableContext,
    ImportContext<Data.Models.Server> serverContext,
    StorageLocationService storageLocationService,
    ArchiveService archiveService) : BaseService(logger)
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

    public async Task<IEnumerable<ImportItemInfo>> GetImportInfoAsync<TRecord>(Guid objectKey, ImportRecordFlags importRecordFlags) where TRecord : Data.Models.BaseModel
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        var importItemInfos = new List<ImportItemInfo>();

        using (var importZip = ZipArchive.Open(importArchivePath))
        {
            gameContext.UseArchive(importZip);
            
            importItemInfos.AddRange();
        }
    }

    public async Task ImportGameAsync(SDK.Models.Manifest.Game record, Guid objectKey, ImportRecordFlags importRecordFlags)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

        using (var importZip = ZipArchive.Open(importArchivePath))
        using (var context = contextFactory.Create<Data.Models.Game>(importZip, importRecordFlags))
        {
            Data.Models.Game game;
            
            if (await context.Games.ExistsAsync(record))
                game = await context.Games.UpdateAsync(record);
            else
                game = await context.Games.AddAsync(record);
            
            context.Use(game);

            if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
                await context.AddToQueueAsync(record.Actions);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
                await context.AddToQueueAsync(record.Archives);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Collections))
                await context.AddToQueueAsync(record.Collections);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.CustomFields))
                await context.AddToQueueAsync(record.CustomFields);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Developers))
                await context.AddToQueueAsync(record.Developers);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Engine))
                await context.AddToQueueAsync(record.Engine);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Genres))
                await context.AddToQueueAsync(record.Genres);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Keys))
                await context.AddToQueueAsync(record.Keys);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Media))
                await context.AddToQueueAsync(record.Media);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.MultiplayerModes))
                await context.AddToQueueAsync(record.MultiplayerModes);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Platforms))
                await context.AddToQueueAsync(record.Platforms);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.PlaySessions))
                await context.AddToQueueAsync(record.PlaySessions);

            if (importRecordFlags.HasFlag(ImportRecordFlags.Publishers))
                await context.AddToQueueAsync(record.Publishers);

            if (importRecordFlags.HasFlag(ImportRecordFlags.Saves))
                await context.AddToQueueAsync(record.Saves);

            if (importRecordFlags.HasFlag(ImportRecordFlags.SavePaths))
                await context.AddToQueueAsync(record.SavePaths);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
                await context.AddToQueueAsync(record.Scripts);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Tags))
                await context.AddToQueueAsync(record.Tags);
        }
    }

    public async Task ImportRedistributableAsync(SDK.Models.Manifest.Redistributable record, Guid objectKey,
        ImportRecordFlags importRecordFlags)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        
        using (var importZip = ZipArchive.Open(importArchivePath))
        using (var context = contextFactory.Create<Data.Models.Redistributable>(importZip, importRecordFlags))
        {
            Data.Models.Redistributable redistributable;
            
            if (await context.Redistributables.ExistsAsync(record))
                redistributable = await context.Redistributables.UpdateAsync(record);
            else
                redistributable = await context.Redistributables.AddAsync(record);
            
            context.Use(redistributable);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Archives))
                await context.AddToQueueAsync(record.Archives);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
                await context.AddToQueueAsync(record.Scripts);
        }
    }

    public async Task ImportServerAsync(SDK.Models.Manifest.Server record, Guid objectKey,
        ImportRecordFlags importRecordFlags)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        
        Data.Models.Server server;
        
        using (var importZip = ZipArchive.Open(importArchivePath))
        using (var context = contextFactory.Create<Data.Models.Server>(importZip, importRecordFlags))
        {
            if (await context.Servers.ExistsAsync(record))
                server = await context.Servers.UpdateAsync(record);
            else
                server = await context.Servers.AddAsync(record);
            
            context.Use(server);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Actions))
                await context.AddToQueueAsync(record.Actions);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.ServerHttpPaths))
                await context.AddToQueueAsync(record.HttpPaths);

            if (importRecordFlags.HasFlag(ImportRecordFlags.ServerConsoles))
                await context.AddToQueueAsync(record.ServerConsoles);
            
            if (importRecordFlags.HasFlag(ImportRecordFlags.Scripts))
                await context.AddToQueueAsync(record.Scripts);
        }
    }
}