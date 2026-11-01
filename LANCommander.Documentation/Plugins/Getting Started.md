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
- The version of LANCommander your plugin targets. The contract packages below are released under the
  same version as the server and launcher, and the framework binds plugins against the host's
  already-loaded copies of them, so build against the version you will run on.

## 1. Create a class library

A plugin is an ordinary class library targeting `net10.0`:

```bash
dotnet new classlib -n MyCompany.MyPlugin -f net10.0
```

## 2. Reference the contract packages

Reference the LANCommander packages that expose the extension points you need. Reference them with
`ExcludeAssets="runtime"` so your plugin binds against the host's already-loaded copies at runtime
rather than shipping (and loading) its own duplicates:

```xml
<ItemGroup>
  <!-- Core plugin contracts, events, and the PowerShell extension point. -->
  <PackageReference Include="LANCommander.SDK" Version="2.2.0" ExcludeAssets="runtime" />

  <!-- Server routes, navigation, page access, package importing and storage locations. -->
  <PackageReference Include="LANCommander.Server.Plugins" Version="2.2.0" ExcludeAssets="runtime" />

  <!-- The server's Blazor controls, for plugin pages. -->
  <PackageReference Include="LANCommander.Server.UI" Version="2.2.0" ExcludeAssets="runtime" />
</ItemGroup>
```

A server plugin does not reference `LANCommander.Server`, `LANCommander.Server.Data` or the other
server assemblies; what the server offers plugins is in `LANCommander.Server.Plugins`. The exception for
now is `IMetadataProvider`, which still lives in `LANCommander.Server.Services`.

Launcher UI extension points are in `LANCommander.Launcher.Plugins`, which is not published as a package
yet; reference it from a LANCommander checkout with `Private="false"`.

:::note
`ExcludeAssets="runtime"` keeps the contract assemblies out of your plugin's output folder. This is
important: the framework preserves type identity across the plugin's load context by deferring these
shared assemblies to the host. Your plugin's *own* private dependencies (NuGet packages, helper libraries)
should ship normally so they land next to your plugin's DLL. The packages the contract packages depend
on (Radzen, for example) are still copied to your output. Unless your own code uses them they are never
loaded, so they can be left out of the drop-in folder.
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

Copying the folder in by hand is fine while developing. To hand a server plugin to administrators,
package it instead (next step).

## 6. Package a server plugin

Administrators install server plugins from **Settings → Plugins**, either by uploading a `.nupkg` or
by picking one from a NuGet package source: nuget.org, or a feed they add themselves. A plugin package
is an ordinary NuGet package with two conventions:

- Its `lib/net10.0/` folder **is** the drop-in folder. It holds the plugin assembly and every private
  dependency the plugin ships. The server does not install package dependencies, so anything the plugin
  needs beyond the host's own assemblies must be in the package.
- It declares the `LANCommander.Plugin` package type, plus one for each host it runs on:
  `LANCommander.Plugin.Server` and/or `LANCommander.Plugin.Launcher`, matching the `Hosts` in its
  `[LANCommanderPlugin]` attribute. The server's Plugins page searches for `LANCommander.Plugin.Server`,
  so a package without it can only be installed by entering its exact ID or by uploading it. The names
  are in [`PluginPackageTypes`](/Plugins/API%20Reference#pluginpackagetypes).

Add this to your plugin's project file. It lists the plugin's own assemblies in `PluginAssembly`, which
are the same files that belong in the drop-in folder, and packs them beside the plugin assembly:

```xml
<PropertyGroup>
  <PackageId>MyCompany.MyPlugin</PackageId>
  <Version>1.0.0</Version>
  <Authors>My Company</Authors>
  <Description>What the plugin adds to a LANCommander server.</Description>

  <!-- LANCommander.Plugin, plus a type per host in [LANCommanderPlugin] -->
  <PackageType>LANCommander.Plugin;LANCommander.Plugin.Server</PackageType>
  <!-- The package carries the plugin's dependencies itself; the host provides the rest -->
  <SuppressDependenciesWhenPacking>true</SuppressDependenciesWhenPacking>
  <NoWarn>$(NoWarn);NU5128</NoWarn>
  <TargetsForTfmSpecificBuildOutput>$(TargetsForTfmSpecificBuildOutput);PackPluginAssemblies</TargetsForTfmSpecificBuildOutput>
</PropertyGroup>

<ItemGroup>
  <!-- The plugin assembly and its private dependencies, by assembly name -->
  <PluginAssembly Include="$(AssemblyName);Humanizer" />
</ItemGroup>

<Target Name="PackPluginAssemblies">
  <ItemGroup>
    <BuildOutputInPackage Include="@(PluginAssembly->'$(OutDir)%(Identity).dll')" Condition="'%(Identity)' != '$(AssemblyName)'" />
  </ItemGroup>
</Target>
```

Then `dotnet pack -c Release` writes the package, and `dotnet nuget push` publishes it to nuget.org or
any other feed.

The official plugins ([Steam](https://github.com/LANCommander/LANCommander.Plugins.Steam),
[GOG](https://github.com/LANCommander/LANCommander.Plugins.GOG) and
[Relay](https://github.com/LANCommander/LANCommander.Plugins.Relay)) pack this way through a
`build/PluginPackage.targets` file you can copy. It also reads the package types from the built
assembly's `[LANCommanderPlugin]` attribute, so they can't drift from `Hosts`, and their
`.github/workflows/Release.yml` publishes to nuget.org whenever a GitHub release is published.

Packages whose ID starts with `LANCommander.Plugins.` and that are owned on nuget.org by the
[`LANCommander` organization](https://www.nuget.org/profiles/LANCommander) are the official LANCommander plugins, and the Plugins page marks them **Official**. The ID prefix
alone proves nothing, since anyone can publish under it, and packages from other feeds or uploaded
by hand are never marked.

Prerelease versions are offered when the administrator turns them on under **Sources**, and always on
a server that is itself a prerelease.

When a package is installed, the server checks it before accepting it. It needs exactly one assembly
with `[LANCommanderPlugin]`, a `Hosts` value that includes the server, and a `MinHostVersion` /
`MaxHostVersion` range that covers the running server. The plugin is installed into a folder named after
that assembly. Installs, updates and uninstalls all take effect the next time the server restarts,
because a loaded plugin's assemblies stay loaded until then.

:::note
Put assemblies directly in `lib/net10.0/`, not in subfolders. Without a `.deps.json` the loader
resolves a plugin's assemblies from the top of its folder only, so assemblies under subfolders such as
`runtimes/` are not found. Other files the plugin reads itself may sit in subfolders.
:::

### Launcher plugins

A launcher plugin is packaged the same way, declaring `LANCommander.Plugin.Launcher` instead of (or as
well as) `LANCommander.Plugin.Server`. Launchers get it in three ways:

- **From the server.** On the server's **Settings → Plugins → Launcher** tab, administrators upload a
  launcher plugin's `.nupkg` or add one from the server's package sources. The server doesn't install
  it: it checks the package is a launcher plugin and keeps it, unchanged, for launchers to download from
  `/api/Launcher/Plugins`. Turning on **Required** makes every launcher install it the next time it signs
  in, and keeps players from uninstalling it.
- **From a NuGet registry.** The launcher's **Settings → Plugins** page lists the registries it searches,
  each with a checkbox to turn it on or off. The server it's connected to is on by default; nuget.org is
  listed but off, and players can add other feeds.
- **From a file**, with **Install from File** on the same page.

The launcher checks a package the way the server does, against its own version and `Hosts`. Installs,
updates and uninstalls take effect the next time the launcher starts, so a required plugin installed
when a player signs in starts working after they restart the launcher.

## 7. Verify it loaded

Start the host and check the logs. A successful load emits an entry like:

```
Loaded plugin 'My Plugin' (com.mycompany.myplugin) v1.0.0 by My Company
```

If your plugin does not appear, the logs will explain why — a missing `[LANCommanderPlugin]` attribute,
an incompatible host version, a host it does not target, or an exception thrown during
`ConfigureServices`. Every failure is isolated and logged rather than crashing the host. On the server,
**Settings → Plugins** also lists the plugin, marked *Running* or *Not loaded*.

## Next steps

Now that your plugin loads, head to [Extension Points](/Plugins/Extension%20Points) to add real
functionality, or browse the [API Reference](/Plugins/API%20Reference) for the complete surface.
