using LANCommander.Server.Plugins.Data;

namespace LANCommander.Server.Startup;

public static class PluginData
{
    /// <summary>
    /// Checks the plugins' database contexts and applies their migrations. Runs after the server's
    /// own migrations and before plugins initialize, so a plugin whose data isn't ready is turned off
    /// before any of its startup code runs.
    /// </summary>
    public static Task PreparePluginDataAsync(this WebApplication app)
        => app.Services.GetRequiredService<PluginDataManager>().PrepareAllAsync();
}
