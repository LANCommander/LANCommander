namespace LANCommander.Server.Services.Models;

public class DockerContainer
{
    public string Id { get; set; }
    public string Name { get; set; }
    public Guid HostId { get; set; }
}