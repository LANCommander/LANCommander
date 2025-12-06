namespace LANCommander.Launcher.Services.Import;

public class ImportStatusUpdate
{
    public int Index { get; set; }
    public int Total { get; set; }
    public string? Error { get; set; }
    public IImportItemInfo? CurrentItem { get; set; }
}