using LANCommander.SDK;
using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class GameServerSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Servers");

    // Seconds to wait after the last player stops a game before stopping its
    // on-player-activity servers. Debounces stop/start thrash when players relaunch.
    public int AutostopDelay { get; set; } = 300;
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