using System.IO.Compression;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace LANCommander.Server.Services.Importers;

/// <summary>
/// Implementations should be able to process a single record. This will be called
/// by the import service to process records in its queue. Only one instance of
/// this needs to exist for each type.
/// </summary>
/// <typeparam name="TRecord"></typeparam>
public interface IImporter<TRecord, TEntity>
{
    Task<ImportItemInfo> InfoAsync(TRecord record);
    bool CanImport(TRecord record);
    bool CanExport(TRecord record);
    Task<TEntity> AddAsync(TRecord record);
    Task<TEntity> UpdateAsync(TRecord record);
    Task<TRecord> ExportAsync(TEntity entity);
    Task<bool> ExistsAsync(TRecord record);
}