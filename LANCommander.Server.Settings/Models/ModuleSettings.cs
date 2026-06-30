using LANCommander.SDK;

namespace LANCommander.Server.Settings.Models;

public class ModuleSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Modules");
}
