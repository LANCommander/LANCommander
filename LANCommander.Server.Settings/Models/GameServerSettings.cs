using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class GameServerSettings
{
    public string StoragePath { get; set; } = "Servers";
    public IEnumerable<ServerEngineConfiguration> ServerEngines { get; set; } = new List<ServerEngineConfiguration>()
    {
        new ServerEngineConfiguration
        {
            Name = "Local",
            Type = ServerEngine.Local,
        },
        new ServerEngineConfiguration
        {
            Name = "Docker",
            Type = ServerEngine.Docker,
            Address = "unix:///var/run/docker.sock",
        }
    };
}