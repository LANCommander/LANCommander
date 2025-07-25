namespace LANCommander.Server.Services.Importers;

public abstract class BaseImporter<TRecord, TEntity> : IImporter<TRecord, TEntity>
{
    protected ImportContext ImportContext { get; private set; }

    public void UseContext(ImportContext context)
    {
        ImportContext = context;
    }
    
    public abstract Task<ImportItemInfo> GetImportInfoAsync(TRecord record);
    public abstract Task<ExportItemInfo> GetExportInfoAsync(TEntity record);
    public abstract bool CanImport(TRecord record);
    public abstract bool CanExport(TRecord record);
    public abstract Task<TEntity> AddAsync(TRecord record);
    public abstract Task<TEntity> UpdateAsync(TRecord record);
    public abstract Task<TRecord> ExportAsync(Guid id);
    public abstract Task<bool> ExistsAsync(TRecord record);
}