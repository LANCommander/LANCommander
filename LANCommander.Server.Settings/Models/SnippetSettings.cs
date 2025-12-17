using LANCommander.SDK;

namespace LANCommander.Server.Settings.Models;

public class SnippetSettings
{
    public string StoragePath { get; set; } = AppPaths.GetConfigPath("Snippets");
}