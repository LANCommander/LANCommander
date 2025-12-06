using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Launcher.Services.Import;

public interface IImporter<TRecord> where TRecord : class
{
    void UseContext(ImportContext importContext);
    Task<ImportItemInfo<TRecord>> GetImportInfoAsync(TRecord record, BaseManifest manifest);
    Task<bool> CanImportAsync(TRecord record);
    Task<bool> AddAsync(ImportItemInfo<TRecord> record);
    Task<bool> UpdateAsync(ImportItemInfo<TRecord> record);
    Task<bool> ExistsAsync(ImportItemInfo<TRecord> record);
}