using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Models;

public class ImportItem
{
    public Guid Id { get; set; }
    public string Name { get; set; }

    public ImportItem(Guid id, string name)
    {
        Id = id;
        Name = name;
    }
}