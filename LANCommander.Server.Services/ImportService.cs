using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services;

public class ImportService<T>(
    ILogger<ImportService<T>> logger,
    IImporter<T> importer,
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
}