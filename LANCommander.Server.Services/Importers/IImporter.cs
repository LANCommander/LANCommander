using System.IO.Compression;

namespace LANCommander.Server.Services.Importers;

public interface IImporter<T>
{
    Task<T> ImportAsync(Guid objectKey, ZipArchive importZip);
}