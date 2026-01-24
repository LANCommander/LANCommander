namespace LANCommander.Server.Settings.Models;

public class ServerSettings
{
    public ArchiveSettings Archives { get; set; } = new();
    public AuthenticationSettings Authentication { get; set; } = new();
    public BackupSettings Backups { get; set; } = new();
    public BeaconSettings Beacon { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public HttpSettings Http { get; set; } = new();
    public IGDBSettings IGDB { get; set; } = new();
    public IPXRelaySettings IPXRelay { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public LibrarySettings Library { get; set; } = new();
    public LogSettings Logs { get; set; } = new();
    public MediaSettings Media { get; set; } = new();
    public RoleSettings Roles { get; set; } = new();
    public ScriptSettings Scripts { get; set; } = new();
    public GameServerSettings GameServers { get; set; } = new();
    public UpdateSettings Update { get; set; } = new();
    public UserSaveSettings UserSaves { get; set; } = new();
}