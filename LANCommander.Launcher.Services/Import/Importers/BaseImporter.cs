using LANCommander.Launcher.Services.Exceptions;

namespace LANCommander.Launcher.Services.Import.Importers;

public abstract class BaseImporter<TRecord, TEntity> : IImporter<TRecord, TEntity> where TRecord : class
{
    protected ImportContext ImportContext { get; private set; }
    
    public void UseContext(ImportContext importContext)
    {
        ImportContext = importContext;
    }

    public async Task<TEntity> ImportAsync(IImportItemInfo importItem)
    {
        if (importItem is ImportItemInfo<TRecord> importItemInfo)
        {
            if (await ExistsAsync(importItemInfo.Record))
            {
                importItem.Processed = true;
                return await UpdateAsync(importItemInfo.Record);
            }

            importItem.Processed = true;
            return await AddAsync(importItemInfo.Record);
        }
        
        throw new ImportSkippedException<TRecord>(null, "Import item record is not supported by this importer.");
    }

    public abstract string GetKey(TRecord record);
    public abstract Task<ImportItemInfo<TRecord>> GetImportInfoAsync(TRecord record);
    public abstract Task<bool> CanImportAsync(TRecord record);
    public abstract Task<TEntity> AddAsync(TRecord record);
    public abstract Task<TEntity> UpdateAsync(TRecord record);
    public abstract Task<bool> ExistsAsync(TRecord record);
}