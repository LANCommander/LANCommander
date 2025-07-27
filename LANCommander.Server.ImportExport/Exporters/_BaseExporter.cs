using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Exporters;

public abstract class BaseExporter<TRecord, TEntity> : IExporter<TRecord, TEntity>
{
    protected ExportContext ExportContext { get; private set; }

    public void UseContext(ExportContext context)
    {
        ExportContext = context;
    }
    
    public abstract Task<ExportItemInfo> GetExportInfoAsync(TEntity record);
    public abstract bool CanExport(TRecord record);
    public abstract Task<TRecord> ExportAsync(Guid id);
}