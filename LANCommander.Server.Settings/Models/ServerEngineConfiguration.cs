using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class ServerEngineConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Local";
    public ServerEngine Type { get; set; } = ServerEngine.Local;
    public string Address { get; set; } = String.Empty;
}