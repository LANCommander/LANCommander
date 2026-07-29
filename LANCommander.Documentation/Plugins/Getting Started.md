---
sidebar_label: Getting Started
sidebar_position: 2
---

# Getting Started

This guide walks through building a minimal plugin from an empty project to a working drop-in that the
launcher loads at startup. If you'd rather read finished code, the repository ships a complete reference
plugin under `LANCommander.SamplePlugin` that exercises every extension point described here.

## Prerequisites

- The .NET 10 SDK.
- A local checkout of the LANCommander source, or a package/binary reference to the contract assemblies
  listed below. The framework binds plugins against the host's already-loaded assemblies, so you build
  against the same contract assemblies the host ships.

## 1. Create a class library

A plugin is an ordinary class library targeting `net10.0`:

```bash
dotnet new classlib -n MyCompany.MyPlugin -f net10.0
```

## 2. Reference the contract assemblies

Reference the LANCommander assemblies that expose the extension points you need. Reference them with
`Private=false` so your plugin binds against the host's already-loaded copies at runtime rather than
shipping (and loading) its own duplicates:

```xml
<ItemGroup>
  <!-- Core plugin contracts, events, and the PowerShell extension point. -->
  <ProjectReference Include="..\LANCommander.SDK\LANCommander.SDK.csproj" Private="false" />

  <!-- Launcher UI extension points (only needed if you extend the launcher UI). -->
  <ProjectReference Include="..\LANCommander.Launcher.Plugins\LANCommander.Launcher.Plugins.csproj" Private="false" />

  <!-- Server contracts such as IMetadataProvider (only needed for server extensions). -->
  <ProjectReference Include="..\LANCommander.Server.Services\LANCommander.Server.Services.csproj" Private="false" />
</ItemGroup>
```

:::note
`Private=false` keeps the contract assemblies out of your plugin's output folder. This is important:
the framework preserves type identity across the plugin's load context by deferring these shared
assemblies to the host. Your plugin's *own* private dependencies (NuGet packages, helper libraries)
should ship normally so they land next to your plugin's DLL.
:::

## 3. Implement the entry point

Every plugin has a single entry point that implements
[`IPlugin`](/Plugins/API%20Reference#iplugin). The two lifecycle methods map directly onto the host's
"build the DI container once" model:

- **`ConfigureServices`** runs while the host is still populating its service collection, *before* the
  provider is built. Only register services here — do not resolve them.
- **`InitializeAsync`** runs *after* the provider is built. Resolve services, subscribe to events, and do
  any asynchronous startup work here. You receive a
  [`PluginContext`](/Plugins/API%20Reference#plugincontext) with the host identity, a scoped service
  provider, the plugin's directory, and a scoped logger.

```csharp
using LANCommander.SDK.Plugins;
using LANCommander.SDK.Plugins.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MyCompany.MyPlugin;

public sealed class MyPlugin : IPlugin
{
    public string Id => "com.mycompany.myplugin";
    public string Name => "My Plugin";
    public string Version => "1.0.0";
    public string Author => "My Company";

    private IDisposable? _launchSubscription;

    public void ConfigureServices(IServiceCollection services)
    {
        // Register anything you'll resolve later, or any extension point implementations.
        // e.g. services.AddSingleton<ISettingsPageExtension, MySettingsExtension>();
    }

    public Task InitializeAsync(PluginContext context, CancellationToken cancellationToken)
    {
        var events = context.Services.GetRequiredService<IPluginEventBus>();

        _launchSubscription = events.Subscribe<GameBeforeLaunchEvent>((evt, ct) =>
        {
            context.Logger.LogInformation("Game {GameId} is about to launch", evt.GameId);
            return Task.CompletedTask;
        });

        context.Logger.LogInformation("{Name} initialized on host {Host}", Name, context.Host);

        return Task.CompletedTask;
    }
}
```

## 4. Mark the assembly as a plugin

Discovery is driven by an assembly-level
[`[LANCommanderPlugin]`](/Plugins/API%20Reference#lancommanderpluginattribute) attribute. It names the
entry point and declares compatibility metadata. Place it anywhere in your project (a common choice is
above the `namespace` declaration in your entry point file):

```csharp
using LANCommander.SDK.Plugins;

[assembly: LANCommanderPlugin(
    typeof(MyCompany.MyPlugin.MyPlugin),
    Id = "com.mycompany.myplugin",
    MinHostVersion = "1.1.0",
    Hosts = PluginHost.Server | PluginHost.Launcher)]
```

- **`Id`** is optional; when omitted the loader falls back to the entry point's `IPlugin.Id`.
- **`MinHostVersion` / `MaxHostVersion`** are optional SemVer bounds (inclusive). A plugin outside the
  host's version range is skipped.
- **`Hosts`** declares which hosts the plugin supports. Defaults to both server and launcher.

## 5. Build and deploy

Build your plugin and copy its output into a subfolder of LANCommander's `Plugins` directory. That
directory lives inside LANCommander's data folder — `Data/Plugins` next to the executable, or under your
user profile's application data if the install directory is not writable:

```
Data/
└── Plugins/
    └── MyCompany.MyPlugin/
        ├── MyCompany.MyPlugin.dll
        ├── MyCompany.MyPlugin.deps.json
        └── (your private dependencies)
```

The loader prefers an assembly named after the folder (`MyCompany.MyPlugin.dll` in the example above),
so naming the folder after your main assembly is the most reliable convention.

## 6. Verify it loaded

Start the host and check the logs. A successful load emits an entry like:

```
Loaded plugin 'My Plugin' (com.mycompany.myplugin) v1.0.0 by My Company
```

If your plugin does not appear, the logs will explain why — a missing `[LANCommanderPlugin]` attribute,
an incompatible host version, a host it does not target, or an exception thrown during
`ConfigureServices`. Every failure is isolated and logged rather than crashing the host.

## Next steps

Now that your plugin loads, head to [Extension Points](/Plugins/Extension%20Points) to add real
functionality, or browse the [API Reference](/Plugins/API%20Reference) for the complete surface.
