using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace LANCommander.Server.Services;

public class ImportService<T>(
    ILogger<ImportService<T>> logger,
    IImporter<T> importer,
    ArchiveService archiveService) : BaseService(logger)
    where T : class, IBaseModel
{
    public async Task<T> ImportFromUploadArchiveAsync(Guid objectKey)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);
        
        using var importZip = ZipFile.OpenRead(importArchivePath);
        return await importer.ImportAsync(objectKey, importZip);
    }

    public async Task<T> ImportFromLocalFileAsync(string localFilePath)
    {
        Guid objectKey = Guid.NewGuid();

        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(objectKey.ToString());

        File.Copy(localFilePath, importArchivePath, true);
        
        return await ImportFromUploadArchiveAsync(objectKey);
    }
}