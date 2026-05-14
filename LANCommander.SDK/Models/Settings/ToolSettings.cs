namespace LANCommander.SDK.Models;

public class ToolSettings
{
    public string InstallDirectory { get; set; } = AppPaths.GetConfigPath("Tools");
}
