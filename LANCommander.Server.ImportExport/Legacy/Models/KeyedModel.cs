namespace LANCommander.Server.ImportExport.Legacy.Models;

internal abstract class KeyedModel : IKeyedModel
{
    public Guid Id { get; set; }
}