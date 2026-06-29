namespace LANCommander.SDK.Models;

public class ModuleSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Modules");
}
