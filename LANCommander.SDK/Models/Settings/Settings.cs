namespace LANCommander.SDK.Models;

public class Settings
{
    public const string DEFAULT_GAME_USERNAME = "Player";
    public const string SETTINGS_FILE_NAME = "Settings.yml";
    public const string SQLITE_DEFAULT_DB_NAME = "LANCommander.db";

    public ArchiveSettings Archives { get; set; } = new();
    public AuthenticationSettings Authentication { get; set; } = new();
    public LauncherSettings Launcher { get; set; } = new();
    public LibrarySettings Library { get; set; } = new();
    public GameSettings Games { get; set; } = new();
    public MediaSettings Media { get; set; } = new();
    public DebugSettings Debug { get; set; } = new();
    public UpdateSettings Updates { get; set; } = new();
    public IPXRelaySettings IPXRelay { get; set; } = new();
    public SteamSettings Steam { get; set; } = new();

    public string Culture { get; set; } = "en-US";
}