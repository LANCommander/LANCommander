using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Importers;

public abstract class BaseImporter<TRecord, TEntity> : IImporter<TRecord, TEntity>
{
    protected ImportContext ImportContext { get; private set; }

    public void UseContext(ImportContext context)
    {
        ImportContext = context;
    }
    
    public abstract Task<ImportItemInfo> GetImportInfoAsync(TRecord record);
    public abstract bool CanImport(TRecord record);
    public abstract Task<TEntity> AddAsync(TRecord record);
    public abstract Task<TEntity> UpdateAsync(TRecord record);
    public abstract Task<bool> ExistsAsync(TRecord record);
}