namespace LANCommander.Server.ImportExport.Legacy.Models;

internal class Redistributable : BaseModel
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Notes { get; set; }
    public DateTime ReleasedOn { get; set; }
    public virtual IEnumerable<Archive> Archives { get; set; }
    public virtual IEnumerable<Script> Scripts { get; set; }
}