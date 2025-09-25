namespace LANCommander.SDK.Models;

public interface ISettings
{
    public AuthenticationSettings Authentication { get; set; }
    public GameSettings Games { get; set; }
    public MediaSettings Media { get; set; }
    public DebugSettings Debug { get; set; }
    public UpdateSettings Updates { get; set; }
}