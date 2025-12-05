namespace LANCommander.Server.ImportExport.Legacy.Models;

internal abstract class BaseModel : KeyedModel
{
    public DateTime CreatedOn { get; set; }
    public DateTime UpdatedOn { get; set; }
}