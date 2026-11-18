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

    /// <summary>
    /// For <see cref="ServerEngine.Remote"/>: whether the host's servers are added here as Remote servers
    /// as they appear on it, filled in with what the host says about them, rather than one by one.
    /// </summary>
    public bool AddServers { get; set; } = true;

    /// <summary>
    /// For <see cref="ServerEngine.Remote"/>: the host's servers that have been added here, so one an
    /// administrator deletes isn't added again.
    /// </summary>
    public List<Guid> AddedServerIds { get; set; } = [];

    public ServerEngineConfiguration()
    {
        
    }
}