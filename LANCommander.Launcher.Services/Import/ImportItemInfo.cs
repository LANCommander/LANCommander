namespace LANCommander.Launcher.Services.Import;

public class ImportItemInfo<T> : IImportItemInfo where T : class
{
    public string Key { get; set; }
    public string Type { get; set; }
    public string Name { get; set; }
    public bool Processed { get; set; }
    public T Record { get; set; }
}