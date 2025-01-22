namespace LANCommander.Launcher.Models;

public class ImportStatusUpdate
{
    public int Index { get; set; }
    public int Total { get; set; }
    public ImportItem CurrentItem { get; set; }
}