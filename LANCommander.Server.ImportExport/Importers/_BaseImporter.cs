using LANCommander.Server.ImportExport.Exceptions;
using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Importers;

public abstract class BaseImporter<TRecord> : IImporter<TRecord> where TRecord : class
{
    protected ImportContext ImportContext { get; private set; }

    public void UseContext(ImportContext context)
    {
        ImportContext = context;
    }

    public async Task<bool> ImportAsync(IImportItemInfo importItem)
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
    public abstract Task<bool> AddAsync(TRecord record);
    public abstract Task<bool> UpdateAsync(TRecord record);
    public abstract Task<bool> ExistsAsync(TRecord record);
}