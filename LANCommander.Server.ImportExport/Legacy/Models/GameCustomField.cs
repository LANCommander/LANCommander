using System.ComponentModel.DataAnnotations;

namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class GameCustomField
{
    public GameCustomField()
    {
    }

    public GameCustomField(string name, string value)
    {
        Name = name;
        Value = value;
    }
    
    [MaxLength(64)]
    public string Name { get; set; }

    [MaxLength(1024)]
    public string Value { get; set; }
}