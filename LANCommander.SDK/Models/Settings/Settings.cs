namespace LANCommander.SDK.Models;

public class Settings : ISettings
{
    public const string DEFAULT_GAME_USERNAME = "Player";
    public const string SETTINGS_FILE_NAME = "Settings.yml";

    public AuthenticationSettings Authentication { get; set; }
    public GameSettings Games { get; set; }
    public MediaSettings Media { get; set; }
    public DebugSettings Debug { get; set; }
    public UpdateSettings Updates { get; set; }
}