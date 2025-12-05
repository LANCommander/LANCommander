using LANCommander.Server.ImportExport.Models;

namespace LANCommander.Server.ImportExport.Importers;

public abstract class BaseImporter<TRecord> : IImporter<TRecord> where TRecord : class
{
    protected ImportContext ImportContext { get; private set; }

    protected List<IImportAsset> AssetQueue { get; } = new();

    public void UseContext(ImportContext context)
    {
        ImportContext = context;
    }

    public async Task<bool> ImportAsync(IImportItemInfo importItem)
    {
        bool result = false;
        
        if (importItem is ImportItemInfo<TRecord> importItemInfo)
        {
            if (await ExistsAsync(importItemInfo.Record))
                result = await UpdateAsync(importItemInfo.Record);
            else
                result = await AddAsync(importItemInfo.Record);

            if (result && importItemInfo.Record is SDK.Models.Manifest.IKeyedModel keyedModel)
                result = await ImportAssetsAsync(keyedModel.Id);
        }

        return result;
    }

    private async Task<bool> ImportAssetsAsync(Guid recordId)
    {
        bool success = true;
        
        var assets = AssetQueue.Where(a => a.RecordId == recordId).ToList();

        foreach (var asset in assets)
        {
            if (await IngestAsync(asset))
                AssetQueue.Remove(asset);
            else
                success = false;
        }

        return success;
    }
    
    public void AddAsset(IImportAsset asset) => AssetQueue.Add(asset);
    
    public abstract string GetKey(TRecord record);
    public abstract Task<ImportItemInfo<TRecord>> GetImportInfoAsync(TRecord record);
    public abstract Task<bool> CanImportAsync(TRecord record);
    public abstract Task<bool> AddAsync(TRecord record);
    public abstract Task<bool> UpdateAsync(TRecord record);
    public abstract Task<bool> IngestAsync(IImportAsset asset);

    public abstract Task<bool> ExistsAsync(TRecord record);
}