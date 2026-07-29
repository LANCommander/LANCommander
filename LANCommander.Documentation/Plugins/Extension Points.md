---
sidebar_label: Extension Points
sidebar_position: 3
---

# Extension Points

This page is a tour of everything a plugin can extend, with a short example for each. For exact
signatures and every available type, see the [API Reference](/Plugins/API%20Reference), which is
generated directly from the source.

## The registration pattern

Almost every extension point follows the same shape: implement an interface, then register your
implementation in [`IPlugin.ConfigureServices`](/Plugins/API%20Reference#iplugin). The host resolves all
registered implementations of a given interface and wires them in automatically.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<ISettingsPageExtension, MySettingsExtension>();
    services.AddSingleton<IContextMenuExtension, MyContextMenuExtension>();
    services.AddSingleton<IPluginPowerShellExtension, MyPowerShellExtension>();
}
```

Where a UI extension point exposes an `Order` property, implementations are sorted ascending, lower
values appear first, and rendered alongside the built-in items.

## Launcher UI extensions

The launcher exposes five UI extension points, all in the
`LANCommander.Launcher.Plugins.Extensions` namespace. Each one builds an Avalonia `Control`.

| Interface | What it adds |
| --- | --- |
| [`INavigationPageExtension`](/Plugins/API%20Reference#inavigationpageextension) | A top-level navigable page reachable from the shell. |
| [`ISettingsPageExtension`](/Plugins/API%20Reference#isettingspageextension) | A section on the settings page. |
| [`IGameDetailTabExtension`](/Plugins/API%20Reference#igamedetailtabextension) | A tab on a game's detail view. |
| [`IContextMenuExtension`](/Plugins/API%20Reference#icontextmenuextension) | Items on a game's context menu. |
| [`IFooterExtension`](/Plugins/API%20Reference#ifooterextension) | A widget in the shell footer. |

:::tip Build controls in code, not XAML
Because plugins load in their own `AssemblyLoadContext`, Avalonia's compiled-XAML asset resolution
(`avares://`) does not reliably resolve across the boundary. The recommended authoring path for plugin
views is to build controls in code, as the examples below do. This is exactly what the reference plugin
does.
:::

### Example: a settings section

```csharp
using Avalonia.Controls;
using Avalonia.Layout;
using LANCommander.Launcher.Plugins.Extensions;

public sealed class MySettingsExtension : ISettingsPageExtension
{
    public string Title => "My Plugin";
    public int Order => 0;

    public Control BuildContent()
    {
        var panel = new StackPanel { Spacing = 8 };

        panel.Children.Add(new TextBlock
        {
            Text = "This settings section was added by my plugin.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        });

        panel.Children.Add(new CheckBox
        {
            Content = "Enable my feature",
            HorizontalAlignment = HorizontalAlignment.Left,
        });

        return panel;
    }
}
```

### Example: game context menu items

`IContextMenuExtension` receives the id of the game the menu was opened for and returns any number of
controls (typically `MenuItem`s) to append:

```csharp
public sealed class MyContextMenuExtension : IContextMenuExtension
{
    public int Order => 0;

    public IEnumerable<Control> BuildMenuItems(Guid gameId)
    {
        var item = new MenuItem { Header = "Do something with this game" };
        item.Click += (_, _) => { /* ... */ };
        yield return item;
    }
}
```

### Example: a navigation page

A navigation page pairs a view model (deriving from
[`PluginViewModelBase`](/Plugins/API%20Reference#pluginviewmodelbase)) with a control that renders it.
The view model type doubles as the registry key used by the shell's content control.

```csharp
public sealed class MyPageExtension : INavigationPageExtension
{
    public string Label => "My Page";
    public int Order => 100;
    public Type ViewModelType => typeof(MyPageViewModel);

    public PluginViewModelBase CreateViewModel() => new MyPageViewModel();

    public Control BuildView() => new TextBlock { Text = "Hello from my page" };
}
```

## Server metadata providers

On the server, a plugin can contribute additional metadata providers by registering an
`IMetadataProvider` (from `LANCommander.Server.Services`). Registered providers are picked up by the
server's provider enumeration and used alongside the built-in ones.

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IMetadataProvider, MyMetadataProvider>();
}
```

## PowerShell extensions

LANCommander runs installs and other tasks through an embedded PowerShell runtime. A plugin can add its
own cmdlets and script modules by implementing
[`IPluginPowerShellExtension`](/Plugins/API%20Reference#ipluginpowershellextension). Registered
extensions are picked up whenever a script runspace is created, in both hosts.

```csharp
using System.Management.Automation;
using LANCommander.SDK.Plugins;

// The cmdlet itself.
[Cmdlet(VerbsCommon.Get, "MyGreeting")]
public sealed class GetMyGreetingCmdlet : PSCmdlet
{
    [Parameter(Position = 0)]
    public string Name { get; set; } = "World";

    protected override void ProcessRecord() => WriteObject($"Hello, {Name}!");
}

// The extension that exposes it to the runspace.
public sealed class MyPowerShellExtension : IPluginPowerShellExtension
{
    public IEnumerable<Type> GetCmdletTypes() => new[] { typeof(GetMyGreetingCmdlet) };

    public IEnumerable<string> GetModulePaths() => Array.Empty<string>();
}
```

Once registered, `Get-MyGreeting` is callable from any LANCommander script. To ship script modules
(`.psm1`/`.psd1`) instead of (or in addition to) cmdlets, return their absolute paths from
`GetModulePaths()`. The plugin directory is available to you via `PluginContext.PluginDirectory`.

## Lifecycle events

The [`IPluginEventBus`](/Plugins/API%20Reference#iplugineventbus) is an in-process, strongly typed
event aggregator registered as a singleton in both hosts. Resolve it in `InitializeAsync` and subscribe
to the events you care about. `Subscribe` returns an `IDisposable`; keep it and dispose it to
unsubscribe.

```csharp
public Task InitializeAsync(PluginContext context, CancellationToken cancellationToken)
{
    var events = context.Services.GetRequiredService<IPluginEventBus>();

    events.Subscribe<GameInstalledEvent>((evt, ct) =>
    {
        context.Logger.LogInformation("Installed {GameId} to {Dir}", evt.GameId, evt.InstallDirectory);
        return Task.CompletedTask;
    });

    return Task.CompletedTask;
}
```

Handlers are awaited and isolated: a throwing handler cannot break the publisher or other subscribers.

The events published today live in the `LANCommander.SDK.Plugins.Events` namespace:

| Event | Raised when |
| --- | --- |
| `GameInstallingEvent` | A game install is about to begin. |
| `GameInstalledEvent` | A game has finished installing. |
| `GameInstallFailedEvent` | A game install fails. |
| `GameUninstallingEvent` | A game is about to be uninstalled. |
| `GameUninstalledEvent` | A game has finished uninstalling. |
| `GameBeforeLaunchEvent` | A game's executable is about to launch. |
| `GameAfterExitEvent` | A launched game process has exited. |
| `InstallQueueChangedEvent` | The install/download queue changes. |
| `UserLoggedInEvent` | A user successfully logs in. |
| `UserLoggedOutEvent` | A user logs out. |

See the [API Reference](/Plugins/API%20Reference#lancommandersdkpluginsevents) for the exact payload of
each event.
