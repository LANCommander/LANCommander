using LANCommander.Launcher.Plugins.Extensions;
using LANCommander.SamplePlugin;
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Plugins.Events;
using LANCommander.Server.Services.Providers.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: LANCommanderPlugin(typeof(SamplePlugin), Id = "com.lancommander.sampleplugin",
    Hosts = PluginHost.Server | PluginHost.Launcher)]

namespace LANCommander.SamplePlugin;

/// <summary>
/// Reference plugin exercising the framework's extension points: a server metadata provider, a
/// launcher settings section, a PowerShell cmdlet, and a lifecycle event subscription. Used as the
/// end-to-end smoke test for the plugin framework.
/// </summary>
public sealed class SamplePlugin : IPlugin
{
    public string Id => "com.lancommander.sampleplugin";
    public string Name => "LANCommander Sample Plugin";
    public string Version => "1.0.0";
    public string Author => "LANCommander";

    private IDisposable? _launchSubscription;

    public void ConfigureServices(IServiceCollection services)
    {
        // Server: add an additional metadata provider that appears in the host's enumeration.
        services.AddSingleton<IMetadataProvider, SampleMetadataProvider>();

        // Launcher: add a settings section rendered by a code-built control.
        services.AddSingleton<ISettingsPageExtension, SampleSettingsExtension>();

        // Both hosts: add a PowerShell cmdlet callable from any script.
        services.AddSingleton<IPluginPowerShellExtension, SamplePowerShellExtension>();
    }

    public Task InitializeAsync(PluginContext context, CancellationToken cancellationToken)
    {
        // Subscribe to a lifecycle event; logs whenever a game is about to launch.
        var eventBus = context.Services.GetRequiredService<IPluginEventBus>();

        _launchSubscription = eventBus.Subscribe<GameBeforeLaunchEvent>((evt, ct) =>
        {
            context.Logger.LogInformation(
                "[SamplePlugin] Game {GameId} is about to launch (action: {Action})", evt.GameId, evt.Action);
            return Task.CompletedTask;
        });

        context.Logger.LogInformation("[SamplePlugin] Initialized on host {Host}", context.Host);

        return Task.CompletedTask;
    }
}
