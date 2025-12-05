namespace LANCommander.Server.ImportExport.Legacy.Models;

internal interface IKeyedModel
{
    Guid Id { get; set; }
}