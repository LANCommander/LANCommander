using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Importers;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace LANCommander.Server.Services;

public class ImportService(
    ILogger<ImportService> logger,
    GameImporter gameImporter,
    ServerImporter serverImporter,
    RedistributableImporter redistributableImporter,
    ArchiveService archiveService) : BaseService(logger)
{
    public async Task<Game> ImportGameAsync(Guid objectKey)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

        using var importZip = ZipFile.OpenRead(importArchivePath);
        return await gameImporter.ImportAsync(objectKey, importZip);
    }

    public async Task<Data.Models.Server> ImportServerAsync(Guid objectKey)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

        using var importZip = ZipFile.OpenRead(importArchivePath);
        return await serverImporter.ImportAsync(objectKey, importZip);
    }

    public async Task<Redistributable> ImportRedistributableAsync(Guid objectKey)
    {
        var importArchive = await archiveService.FirstOrDefaultAsync(a => a.ObjectKey == objectKey.ToString());
        var importArchivePath = await archiveService.GetArchiveFileLocationAsync(importArchive);

        using var importZip = ZipFile.OpenRead(importArchivePath);
        return await redistributableImporter.ImportAsync(objectKey, importZip);
    }
}