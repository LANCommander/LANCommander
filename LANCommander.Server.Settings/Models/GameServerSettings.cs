using LANCommander.SDK;
using LANCommander.Server.Settings.Enums;

namespace LANCommander.Server.Settings.Models;

public class GameServerSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Servers");

    // Seconds to wait after the last player stops a game before stopping its
    // on-player-activity servers. Debounces stop/start thrash when players relaunch.
    public int AutostopDelay { get; set; } = 300;

    // Seconds without a keepalive from the launcher before an active play session is considered
    // stale and ended. Should comfortably exceed the launcher's keepalive interval.
    public int KeepAliveTimeout { get; set; } = 120;

    // Seconds between sweeps that end stale play sessions.
    public int KeepAliveSweepInterval { get; set; } = 30;
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