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
    void UseContext(ImportContext context);
    Task<ImportItemInfo> GetImportInfoAsync(TRecord record);
    Task<ExportItemInfo> GetExportInfoAsync(TEntity record);
    bool CanImport(TRecord record);
    bool CanExport(TRecord record);
    Task<TEntity> AddAsync(TRecord record);
    Task<TEntity> UpdateAsync(TRecord record);
    Task<TRecord> ExportAsync(Guid id);
    Task<bool> ExistsAsync(TRecord record);
}