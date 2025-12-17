using LANCommander.SDK;
using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class GameServerSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Servers");
    public IEnumerable<ServerEngineConfiguration> ServerEngines { get; set; } = 
    [
        new()
        {
            Name = "Local",
            Type = ServerEngine.Local,
        },
        new()
        {
            Name = "Docker",
            Type = ServerEngine.Docker,
            Address = "unix:///var/run/docker.sock",
        }
    ];
}