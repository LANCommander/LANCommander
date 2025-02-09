namespace LANCommander.Launcher.Models;

public class ImportProgress
{
    public int Index { get; set; }
    public int Total { get; set; }
    public ImportItem CurrentItem { get; set; }
    public bool IsImporting { get; set; }
} 