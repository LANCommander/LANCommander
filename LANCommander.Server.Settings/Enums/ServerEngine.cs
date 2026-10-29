namespace LANCommander.Server.Settings.Enums;

public enum ServerEngine
{
    Local,

    /// <summary>
    /// No longer built in: Docker is the LANCommander.Plugins.Docker plugin's engine. Kept so settings
    /// written before can still be read, and the plugin can take their Docker hosts over.
    /// </summary>
    Docker,

    Remote = 2,

    /// <summary>A plugin's engine, named by the server's <c>EngineExtensionId</c>.</summary>
    Extension = 3,
}
