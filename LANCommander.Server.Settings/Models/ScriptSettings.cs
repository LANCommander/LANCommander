namespace LANCommander.Server.Settings.Models;

public class ScriptSettings
{
    public bool EnableAutomaticRepackaging { get; set; } = false;
    public int RepackageEvery { get; set; } = 24;
}