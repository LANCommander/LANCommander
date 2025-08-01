using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Importers;

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
    bool CanImport(TRecord record);
    Task<TEntity> AddAsync(TRecord record);
    Task<TEntity> UpdateAsync(TRecord record);
    Task<bool> ExistsAsync(TRecord record);
}