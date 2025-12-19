using LANCommander.Launcher.Services.Exceptions;
using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Launcher.Services.Import.Importers;

public abstract class BaseImporter<TRecord> : IImporter<TRecord> where TRecord : class
{
    protected ImportContext? ImportContext { get; private set; }
    
    public void UseContext(ImportContext importContext)
    {
        ImportContext = importContext;
    }

    public async Task<bool> ImportAsync(IImportItemInfo importItem)
    {
        if (importItem is ImportItemInfo<TRecord> importItemInfo)
        {
            if (await ExistsAsync(importItemInfo))
            {
                importItem.Processed = true;
                return await UpdateAsync(importItemInfo);
            }

            importItem.Processed = true;
            return await AddAsync(importItemInfo);
        }
        
        throw new ImportSkippedException<TRecord>(null, "Import item record is not supported by this importer.");
    }

    public abstract string GetKey(TRecord record);
    public abstract Task<ImportItemInfo<TRecord>> GetImportInfoAsync(TRecord record, BaseManifest manifest);
    public abstract Task<bool> CanImportAsync(TRecord record);
    public abstract Task<bool> AddAsync(ImportItemInfo<TRecord> importItemInfo);
    public abstract Task<bool> UpdateAsync(ImportItemInfo<TRecord> importItemInfo);
    public abstract Task<bool> ExistsAsync(ImportItemInfo<TRecord> importItemInfo);
}