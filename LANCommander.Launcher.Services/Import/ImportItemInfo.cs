using LANCommander.SDK.Models.Manifest;

namespace LANCommander.Launcher.Services.Import;

public class ImportItemInfo<T> : IImportItemInfo where T : class
{
    public string Key { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
    public bool Processed { get; set; }
    public BaseManifest Manifest { get; set; }
    public T Record { get; set; }
    public int DeferCount { get; set; }
    public Guid? GameId { get; set; }

    /// <summary>
    /// Cached local entity loaded during ExistsAsync to avoid redundant DB lookups.
    /// </summary>
    public object? ExistingEntity { get; set; }
}