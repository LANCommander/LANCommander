using System.ComponentModel.DataAnnotations;

namespace LANCommander.SDK.Models.Manifest;

public class GameCustomField : BaseModel
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