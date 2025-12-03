namespace LANCommander.Launcher.Services.Import;

public class ImportStatusUpdate
{
    public int Index { get; set; }
    public int Total { get; set; }
    public required IImportItemInfo CurrentItem { get; set; }
}