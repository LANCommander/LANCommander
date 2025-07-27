using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Exporters;

/// <summary>
/// Implementations should be able to process a single record. This will be called
/// by the import service to process records in its queue. Only one instance of
/// this needs to exist for each type.
/// </summary>
/// <typeparam name="TRecord"></typeparam>
public interface IExporter<TRecord, TEntity>
{
    void UseContext(ExportContext context);
    Task<ExportItemInfo> GetExportInfoAsync(TEntity record);
    bool CanExport(TRecord record);
    Task<TRecord> ExportAsync(Guid id);
}