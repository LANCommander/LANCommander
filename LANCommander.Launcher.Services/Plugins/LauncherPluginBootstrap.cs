using LANCommander.PluginManagement;
using LANCommander.SDK.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Services.Plugins;

public static class LauncherPluginBootstrap
{
    /// <summary>
    /// Makes the plugin changes staged since the last start, while none of the plugins' files are
    /// loaded, then discovers plugins and lets them register services. Must be the last registration,
    /// since the provider is built straight after.
    /// </summary>
    /// <param name="applyPending">
    /// False for a process that may run beside the launcher's window, such as a headless run for an
    /// elevated script: the window has the plugins loaded, so their folders can't be replaced.
    /// </param>
    public static PluginLoaderService AddLauncherPlugins(this IServiceCollection services, bool applyPending = true)
    {
        var staging = PluginStaging.CreateDefault();

        if (applyPending)
        {
            using var loggerFactory = LoggerFactory.Create(logging => logging.AddSimpleConsole());

            staging.ApplyPending(loggerFactory.CreateLogger("LANCommander.Plugins"));
        }

        services.AddSingleton(staging);
        services.AddSingleton<LauncherPluginService>();

        return PluginBootstrap.ConfigurePlugins(services, PluginHost.Launcher);
    }
}
