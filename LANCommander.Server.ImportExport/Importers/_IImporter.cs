using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Importers;

/// <summary>
/// Implementations should be able to process a single record. This will be called
/// by the import service to process records in its queue. Only one instance of
/// this needs to exist for each type.
/// </summary>
/// <typeparam name="TRecord"></typeparam>
public interface IImporter<TRecord> where TRecord : class
{
    void UseContext(ImportContext context);
    Task<ImportItemInfo<TRecord>> GetImportInfoAsync(TRecord record);
    Task<bool> CanImportAsync(TRecord record);
    Task<bool> ImportAsync(IImportItemInfo importItem);
    Task<bool> AddAsync(TRecord record);
    Task<bool> UpdateAsync(TRecord record);
    Task<bool> ExistsAsync(TRecord record);
}