using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class ServerEngineConfiguration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Local";
    public ServerEngine Type { get; set; } = ServerEngine.Local;
    public string Address { get; set; } = String.Empty;
    public string AccessToken { get; set; } = String.Empty;
    public string RefreshToken { get; set; } = String.Empty;
    public DateTime TokenExpiration { get; set; }

    public ServerEngineConfiguration()
    {
        
    }
}