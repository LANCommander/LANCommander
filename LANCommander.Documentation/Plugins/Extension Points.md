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

## Server media sources

A plugin can add a source to the server's media search, the one behind a game's Media tab, by
registering an [`IMediaGrabberExtension`](/Plugins/API%20Reference#imediagrabberextension). Its
results are shown alongside those of HQ, SteamGridDB and YouTube, under the extension's `Name`, which
must not be one the server already uses. The extension only searches: when an administrator picks a
result, the server downloads its `SourceUrl`.

```csharp
public sealed class MyMediaGrabber : IMediaGrabberExtension
{
    public string Name => "My Source";

    public IReadOnlyCollection<MediaType> SupportedMediaTypes => [MediaType.Cover, MediaType.Logo];

    public async Task<IReadOnlyList<MediaGrabberItem>> SearchAsync(
        MediaType type, string keywords, int page, CancellationToken cancellationToken)
    {
        // Look up keywords and return one MediaGrabberItem per image
    }
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IMediaGrabberExtension, MyMediaGrabber>();
}
```

A source that returns results in pages sets `SupportsPaging`; otherwise the server asks only for
page 0. A source that throws is shown with its error, and the other sources' results still appear.

## Server UI extensions

Server plugins can contribute routable Razor components and navigation entries through the
`LANCommander.Server.Plugins` contracts.

Register the assembly containing your `@page` components:

```csharp
services.AddSingleton<IServerRouteAssemblyExtension>(
    new MyRouteAssemblyExtension(typeof(MyPlugin).Assembly));
```

Then register one or more navigation entries:

```csharp
public sealed class MyNavigationExtension : IServerNavigationExtension
{
    public string Id => "com.mycompany.myplugin.catalog";
    public string Label => "My Catalog";
    public string Href => "/Plugins/MyCatalog";
    public string? Icon => "Package";
    public int Order => 100;
    public PluginAccessPolicy Access => PluginAccessPolicy.Administrator;
}
```

### Placing navigation entries

By default an entry is shown in the plugin area at the end of the navigation, ordered by `Order`
among the other plugin entries. To place it elsewhere, set `Section` to one of the
`ServerNavigationSection` constants, and `After` to the built-in entry it should follow:

| Section | Where |
| --- | --- |
| `Main` | The top level, alongside Games, Servers and Settings |
| `Dashboards` | The Dashboards group |
| `Metadata` | The Metadata group |
| `Scripting` | The Scripting group |
| `Pages` | The Pages group |
| `Settings` | The Settings group |
| `Integrations` | The Integrations group inside Settings |

`After` names a built-in link by its route, or a built-in group by its section:

```csharp
public sealed class RoleSyncNavigationExtension : IServerNavigationExtension
{
    public string Id => "com.mycompany.myplugin.rolesync";
    public string Label => "Role Sync";
    public string Href => "/Plugins/RoleSync";
    public string? Section => ServerNavigationSection.Settings;
    public string? After => "/Settings/Users"; // between Users and Roles
}
```

`ServerNavigationAnchor.Start` places the entry first in its section. With no `After`, or an `After`
the section does not show, the entry is placed last in the section. Entries at the same spot are
ordered by `Order`, then by label. Entries follow built-in entries only, not other plugins' entries.

Every section other than `Main` and `Dashboards` is shown only to administrators, and an entry
placed in a section the user cannot see is not shown to them. Place entries meant for other users in
`Main`, `Dashboards`, or the plugin area. An entry whose section is not recognized is shown in the
plugin area, and a warning is logged.

### Page access

Plugin routes are authorized by the server, not by the plugin. A routable plugin component with no
declaration is reachable only by administrators, so forgetting to declare access produces a locked
page rather than a public one.

Widen access deliberately with `[PluginAccess]`:

```csharp
[PluginAccess(PluginAccessLevel.AuthenticatedUser)]  // any signed-in user
[PluginAccess("Curator", "Moderator")]               // operator-defined roles
```

Administrators satisfy every policy, and anonymous visitors satisfy none. Only the administrator role
ships with LANCommander; every other role is created by the server operator, so a plugin that names a
role must tolerate that role not existing on a given server — the policy simply is not satisfied.

Any `[Authorize]` attribute on the component still applies on top of the server policy, so the
stricter of the two wins. Navigation visibility is not authorization: an entry is shown only when the
user satisfies both the entry's own `Access` and the policy of the page it points at. Plugin
navigation routes must live under `/Plugins/`; duplicate navigation ids and routes are rejected rather
than rendered ambiguously.

## Server API endpoints

A server plugin can add HTTP endpoints, for example for launchers or scripts to call, by registering an
[`IServerEndpointExtension`](/Plugins/API%20Reference#iserverendpointextension). The server maps them
under `/api/Plugins/{RoutePrefix}`, so they cannot collide with the server's own API or with another
plugin's endpoints, and callers authenticate the same way as for the rest of the API: a launcher's
bearer token or a signed-in browser session.

```csharp
public sealed class MyEndpoints : IServerEndpointExtension
{
    public string RoutePrefix => "MyPlugin";

    public PluginAccessPolicy Access => PluginAccessPolicy.AuthenticatedUser;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        // GET /api/Plugins/MyPlugin/Status
        endpoints.MapGet("/Status", (MyStatusService status) => status.Get());

        // POST /api/Plugins/MyPlugin/Things, as the signed-in user
        endpoints.MapPost("/Things", (CreateThing request, ClaimsPrincipal user, MyThingService things) =>
            things.Create(request, user.Identity!.Name!));
    }
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IServerEndpointExtension, MyEndpoints>();
}
```

`MapEndpoints` receives a route group already rooted at the plugin's prefix, so routes are relative
to it. Handlers are ordinary ASP.NET Core minimal API handlers: parameters are bound from the route,
query, body and the server's services.

Access works as it does for pages. The server applies `Access` to every endpoint the extension maps,
before the handler runs, and it defaults to administrators only, so an extension that declares
nothing has locked endpoints rather than open ones. Within the group, `RequireAuthorization` narrows
a single endpoint further, and `AllowAnonymous` opens one to callers who are not signed in; both are
visible in the plugin's source.

A route prefix is a single path segment of letters, digits, `.`, `-` and `_`, starting with a letter
or digit. Extensions with an invalid prefix are ignored, and so are all extensions sharing a prefix,
ignoring case, with a warning in the server log. An extension that throws while mapping does not stop
the server or other plugins from starting.

## Importing generated game packages

The SDK `GameClient.ImportAsync` method is intended for remote clients: it uploads a package over
HTTP and then calls the server import endpoint. A plugin already running inside the server should
instead inject [`IPackageImporter`](/Plugins/API%20Reference#ipackageimporter) from
`LANCommander.Server.Plugins` into the plugin page or another scoped plugin service and invoke the
import pipeline directly:

```csharp
@inject IPackageImporter Importer

await using var package = File.OpenRead(packagePath);
var result = await Importer.ImportStreamAsync(
    package,
    storageLocationId,
    ManifestType.Game,
    copyProgress: progress,
    cancellationToken: cancellationToken);
```

`ImportStreamAsync` copies the stream to controlled temporary storage, enforces a maximum size, and
then runs the same initialize, queue, and commit pipeline as the server API. `ImportFileAsync` is
also available when the LCX already exists on the server. Both return the id of the record the
import created or updated.

To let an administrator choose where an import writes its archives, list the server's storage
locations with [`IStorageLocationProvider`](/Plugins/API%20Reference#istoragelocationprovider):

```csharp
@inject IStorageLocationProvider StorageLocations

var locations = await StorageLocations.GetAsync(StorageLocationType.Archive);
```

The server registers both services. Plugins should use them instead of resolving the server's
database services or import internals, which are not part of the plugin surface.
`PluginContext.Services` is scoped to `InitializeAsync` and must not be retained for later imports.

## Multiplayer lobbies

A launcher plugin can offer lobbies to join, such as the ones a player's friends are hosting on
another platform, by registering an [`ILobbyProvider`](/Plugins/API%20Reference#ilobbyprovider).
The launcher asks every provider when it lists the actions of an installed game that has a
multiplayer mode using the Lobby network protocol. Each lobby returned becomes a "Join *host*'s
lobby" action that runs the game's primary action with the lobby's `JoinArguments` appended.

```csharp
public sealed class MyLobbyProvider : ILobbyProvider
{
    public async Task<IEnumerable<GameLobby>> GetLobbiesAsync(LobbyQuery query, CancellationToken cancellationToken)
    {
        var lobbies = await FindFriendLobbiesAsync(query.InstallDirectory, cancellationToken);

        return lobbies.Select(lobby => new GameLobby(lobby.Id, lobby.HostName, $"+connect_lobby {lobby.Id}"));
    }
}

services.AddSingleton<ILobbyProvider, MyLobbyProvider>();
```

`LobbyQuery` carries the game's id, its install directory, and the manifests of the game and its
installed addons. Providers are queried at the same time and each has a few seconds to answer; one
that throws or runs out of time is logged and skipped without affecting the others. If a provider
holds resources open for a game, such as a platform client, release them on `GameAfterExitEvent`.

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
