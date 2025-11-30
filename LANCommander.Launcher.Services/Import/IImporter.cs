namespace LANCommander.Launcher.Services.Import;

public interface IImporter<TRecord, TEntity> where TRecord : class
{
    void UseContext(ImportContext importContext);
    Task<ImportItemInfo<TRecord>> GetImportInfoAsync(TRecord record);
    Task<bool> CanImportAsync(TRecord record);
    Task<TEntity> AddAsync(TRecord record);
    Task<TEntity> UpdateAsync(TRecord record);
    Task<bool> ExistsAsync(TRecord record);
}