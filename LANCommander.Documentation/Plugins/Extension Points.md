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

A plugin can add a source of game metadata by registering an `IMetadataProviderExtension`. It is
offered alongside HQ, IGDB and PCGamingWiki wherever administrators look a game up, in the launcher's
packaging wizard, and to other plugins through `IMetadataLookup`. Its `Name` must not be one the server
already uses; a plugin source named like a built-in provider is ignored. Set `IsAvailable` to false
while the source can't be used, for example before it has been given an API key, and it isn't listed.

```csharp
public sealed class MyMetadataProvider : IMetadataProviderExtension
{
    public string Name => "My Source";

    public async Task<MetadataSearchPage> SearchGamesAsync(
        string input, string? subProvider, int limit, int offset, CancellationToken cancellationToken)
    {
        // Search for input and return a MetadataGame per result: the source's id and what it knows
    }

    public async Task<Game?> GetGameAsync(string gameId, CancellationToken cancellationToken)
    {
        // Fetch full metadata for an id a search returned, or null
    }
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IMetadataProviderExtension, MyMetadataProvider>();
}
```

Two members are optional. `GetSubProvidersAsync` lists parts of the source a search can be narrowed
to, whose `Slug` is then passed to `SearchGamesAsync`. `ResolveByExternalIdAsync` finds a game by its
id on another service, such as a Steam AppID, so games that already have one are matched exactly
instead of by title.

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

## Page embeds

Pages written on the server can embed things on a line of their own, such as `{{game:<id>}}` or
`{{toc}}`. A plugin adds a kind of its own, shown as one of its Razor components, by registering an
[`IPageEmbedExtension`](/Plugins/API%20Reference#ipageembedextension). Its `Kind` is what authors
write between the braces: letters only, matched without regard to case, and not one of the built-in
kinds (game, server, redistributable, page, children, toc) or another plugin's. The page editor's
Insert menu lists it by its `Name`.

```csharp
public sealed class LeaderboardEmbed : IPageEmbedExtension
{
    public string Kind => "leaderboard";

    public string Name => "Leaderboard";

    public Type Component => typeof(Leaderboard);

    // {{leaderboard:<argument>}}; the editor asks the author to pick one of the choices
    public bool TakesArgument => true;

    public Task<IReadOnlyList<PageEmbedChoice>> GetChoicesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PageEmbedChoice>>([new("kills", "Most kills"), new("wins", "Most wins")]);
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IPageEmbedExtension, LeaderboardEmbed>();
}
```

The component is given the embed as its `Embed` parameter, a `PageEmbed` with the `Kind`, the
`Argument` as written, the `PageId` (null in the site's header and footer) and whether the visitor
`IsAdministrator`. An extension whose component has no such parameter isn't offered.

```razor
@inject IMatchStatisticsProvider Statistics

@if (_rows != null)
{
    <div class="page-embed">
        <div class="page-embed-title">@Embed.Argument</div>
        @* ... *@
    </div>
}
else if (Embed.IsAdministrator)
{
    <div class="page-embed page-embed-missing">No leaderboard called @Embed.Argument</div>
}

@code {
    [Parameter] public PageEmbed Embed { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        // Load what the embed shows from Embed.Argument
    }
}
```

The component is rendered interactively wherever the page is shown: on the page, in the site's header
and footer, and in the editor's preview. It loads what it shows itself and decides what to show when
the argument names nothing. Pages are public, so show only what any visitor may see unless
`IsAdministrator` is set. A component that throws shows nothing to visitors and its error to
administrators, and the rest of the page still appears.

Embeds of a plugin's kind are only shown at the top level of a page, not inside a list or a quote,
where administrators see a note instead. The built-in embed styles can be reused through the classes
`page-embed`, `page-embed-body`, `page-embed-title`, `page-embed-meta` and `page-embed-description`;
each component is wrapped in a `div` with the classes `page-embed-plugin` and
`page-embed-plugin-{kind}`. An argument can't contain whitespace or braces, so choices that do aren't
offered.

## Server engines

The server runs game servers as processes on its own machine (the Local engine) or through another
LANCommander server (Remote). A plugin can run them somewhere else, such as in containers, by
registering an [`IServerEngineExtension`](/Plugins/API%20Reference#iserverengineextension). It is
offered as an engine on a server's General page, and the server's start and stop buttons, autostart,
Monitor, consoles, actions and scripts all work through it.

```csharp
public sealed class MyEngine(MyClient client) : IServerEngineExtension
{
    public string Id => "com.mycompany.myplugin.engine";
    public string Name => "My Cloud";
    public ServerEngineFeatures Features => ServerEngineFeatures.Output;
    public Type? EditorComponent => typeof(MyServerEditor);

    public Task StartAsync(ServerEngineServer server, CancellationToken cancellationToken)
        => client.StartAsync(server.GetSettings<MyServerSettings>().InstanceId, cancellationToken);

    public Task StopAsync(ServerEngineServer server, CancellationToken cancellationToken)
        => client.StopAsync(server.GetSettings<MyServerSettings>().InstanceId, cancellationToken);

    public async Task<ServerEngineStatus> GetStatusAsync(ServerEngineServer server, CancellationToken cancellationToken)
        => await client.IsRunningAsync(server.GetSettings<MyServerSettings>().InstanceId, cancellationToken)
            ? ServerEngineStatus.Running
            : ServerEngineStatus.Stopped;
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IServerEngineExtension, MyEngine>();
}
```

The `Id` is stored with every server that uses the engine, so it must never change. A server whose
engine's plugin isn't installed says so on its General page, and can't be started.

### What the engine keeps about a server

The server keeps a server's name, game, address, autostart, consoles and so on. Anything else the
engine needs, such as which container a server is, goes in the server's engine settings: JSON the
server stores with the server and hands the engine as `ServerEngineServer.Settings` on every call.
Read it with `GetSettings<T>()`. Because the settings travel with the server, they are exported and
imported with it, and go when it is deleted.

The engine's `EditorComponent` edits them. It is shown on the General page in place of the Local
engine's executable and process options, and gets a
[`ServerEngineEditorContext`](/Plugins/API%20Reference#serverengineeditorcontext) as its `Context`
parameter. Its changes join the page's other unsaved changes, and are saved or discarded with them:

```razor
@code {
    [Parameter] public ServerEngineEditorContext Context { get; set; } = default!;

    void Choose(string instanceId)
    {
        Context.SetSettings(new MyServerSettings { InstanceId = instanceId });

        // Optionally fill in the address players connect to
        Context.SetAddress("my-cloud.example", 27015);
    }
}
```

### Configuring the engine

What the engine needs regardless of server, such as the hosts it runs servers on, is configured on
**Settings › Servers**, where the engine is listed under its name with the built-in Local and Remote
engines. Set `SettingsComponent` to a component that edits it:

```csharp
public Type? SettingsComponent => typeof(MyEngineSettings);
```

The plugin keeps the configuration itself, typically in [`IPluginSettings<T>`](#plugin-settings).
The component gets a [`ServerEngineSettingsContext`](/Plugins/API%20Reference#serverenginesettingscontext)
as its `Context` parameter; register with `OnSave` to save the configuration when the administrator
saves the page:

```razor
@implements IDisposable
@inject IPluginSettings<MyEngineConfiguration> Settings

@code {
    [Parameter] public ServerEngineSettingsContext Context { get; set; } = default!;

    // Edited on the page, written only when it is saved
    MyEngineConfiguration _editing = new();
    IDisposable? _onSave;

    protected override void OnInitialized()
    {
        _editing = Settings.Current.Clone();

        _onSave = Context.OnSave(async cancellationToken =>
        {
            Settings.Update(current => current.CopyFrom(_editing));

            await Settings.FlushAsync(cancellationToken);
        });
    }

    public void Dispose() => _onSave?.Dispose();
}
```

The component is created when its panel is opened and disposed when it is closed, so dispose the
registration with it, and keep unsaved changes somewhere longer-lived if they should survive the
panel being closed. A save that throws is reported under the engine's name, and the rest of the
page, including other engines, is saved regardless. A component that throws while it is drawn is
shown as an error in its own panel without affecting the rest of the page. An engine with no
`SettingsComponent` isn't listed.

### Pages of the engine's own

An engine can also add pages to the editors of the servers that use it, listed under its name in the
editor's menu. Each [`ServerEngineSection`](/Plugins/API%20Reference#serverenginesection) is a
component taking the `ServerEngineServer` as its `Server` parameter. Such a page saves its own
changes; to change the server's engine settings, use
[`IServerProvider`](/Plugins/API%20Reference#iserverprovider).

### Running servers

`StartAsync` returns once the server has started, not when it stops. The server runs the server's
Before Start and After Stop scripts around `StartAsync` and `StopAsync`, and a start that throws is
shown to the administrator with its message. `GetStatusAsync` is called every couple of seconds while
anyone is looking at the server, so keep it cheap; one that throws shows the server as failed.

`InitializeAsync` gives the engine an [`IServerEngineHost`](/Plugins/API%20Reference#iserverenginehost)
to report what its servers do on their own: `ReportStatus` when one stops or fails without being asked,
and `WriteOutput` for each line of output, shown on the server's Monitor. `ServersChangedAsync` is
called with every server using the engine at startup and whenever one is added, changed or deleted,
which is the time to start or stop following a server's output.

`Features` declares what else the engine can do. `Output` shows the Monitor's output; `Input` lets
commands typed on the Monitor reach `SendInputAsync`; `ProcessInfo` has the Monitor show the CPU,
memory and uptime `GetProcessInfoAsync` samples. RCON consoles work with any engine, since the server
sends RCON commands itself.

An engine with the `Address` feature knows where players reach its servers, e.g. the address and
published port a container was given, and reports it with `IServerEngineHost.ReportAddress` whenever
it may have changed, such as once a server has started. Pass null for a part it doesn't know, and an
empty host for the LANCommander server's own address. A server's own Host and Port are overrides:
players, launchers and pages get the reported host where the administrator left Host blank, and the
reported port where they left Port at 0. Reports aren't saved, so report again for servers already
running when the engine starts. `ServerEngineServer.Host` and `Port` are the administrator's values,
not the report, and an editor that calls `ServerEngineEditorContext.SetAddress` sets overrides, so
leave the parts the engine reports unset.

### Consoles

An engine with the `Consoles` feature can offer consoles of its own for each server, listed under the
server's Monitor beside the process output and its log file consoles. `GetConsoles` lists them as
[`ServerEngineConsole`](/Plugins/API%20Reference#serverengineconsole)s; it is called whenever the
server's pages are drawn, so work the list out from the server's settings rather than asking anything.
Only consoles in the list can be opened, and a console that can't be opened right now (the server
isn't running, say) is better listed and failing with a message than missing.

A `Log` console is read with `ReadLogAsync`: the latest `Tail` lines, then, when `Follow` is set, new
lines as they are written until the log ends or the page is left. It is shown like the process output,
with the server's command bar under it. A `Terminal` console is opened with `OpenTerminalAsync` at the
size of the administrator's terminal, and returns an
[`IServerEngineTerminal`](/Plugins/API%20Reference#iserverengineterminal): raw bytes both ways, so the
engine's prompt, colours and full-screen programs work, and `ResizeAsync` as the window changes size.
The session is disposed when the administrator leaves the page.

```csharp
public IReadOnlyList<ServerEngineConsole> GetConsoles(ServerEngineServer server) =>
[
    new("logs", "Container log", ServerEngineConsoleKind.Log),
    new("shell", "Shell", ServerEngineConsoleKind.Terminal),
];

public async IAsyncEnumerable<ServerEngineLogLine> ReadLogAsync(ServerEngineServer server, string consoleId,
    ServerEngineLogRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
{
    await foreach (var (time, text) in client.ReadLogAsync(server.GetSettings<MySettings>().Container, request.Tail, request.Follow, cancellationToken))
        yield return new ServerEngineLogLine(text, time);
}

public async Task<IServerEngineTerminal> OpenTerminalAsync(ServerEngineServer server, string consoleId,
    ServerEngineTerminalSize size, CancellationToken cancellationToken) =>
    new MyShell(await client.ExecAsync(server.GetSettings<MySettings>().Container, ["/bin/sh"], size, cancellationToken));
```

Monitor pages are for administrators only. A terminal gives whoever opens it whatever the engine's
session can do, so the server logs who opened which.

### Files

An engine with the `Files` feature lets administrators manage each server's files on its Files page:
browse folders, edit text files such as its configuration, upload maps and mods, create folders and
delete. A Local server's files are its working directory; an engine decides where its servers' files
are, e.g. a container's data volume, and implements `ListFilesAsync`, `OpenFileAsync`,
`WriteFileAsync`, `DeleteFileAsync` and `CreateDirectoryAsync` over them.

Paths are relative to the server's files, with `/` between folders and `""` for the top. The server
checks every path before passing it on, so none starts with `/` or has `..`, `.`, empty segments,
backslashes or colons; still, keep a path from resolving outside the server's files, e.g. through a
symbolic link. `ListFilesAsync` returns [`ServerEngineFile`](/Plugins/API%20Reference#serverenginefile)s
named within the folder; any other names are left out. Writing replaces a file and creates the
folders it is in, and deleting a folder deletes what is in it.

```csharp
public async Task<IReadOnlyList<ServerEngineFile>> ListFilesAsync(ServerEngineServer server, string path,
    CancellationToken cancellationToken) =>
    (await client.ListAsync(server.GetSettings<MySettings>().Volume, path, cancellationToken))
        .Select(entry => new ServerEngineFile(entry.Name, entry.IsDirectory, entry.Size, entry.ModifiedUtc))
        .ToList();

public Task WriteFileAsync(ServerEngineServer server, string path, Stream content,
    CancellationToken cancellationToken) =>
    client.UploadAsync(server.GetSettings<MySettings>().Volume, path, content, cancellationToken);
```

The page waits on these calls, so give up and throw rather than hang when the files can't be reached;
the message is shown. Text files up to 4 MB are edited in the browser and written back in the encoding
they were read in; anything else is uploaded.

## Looking up metadata

To consume the server's configured metadata providers, inject
[`IMetadataLookup`](/Plugins/API%20Reference#imetadatalookup) from `LANCommander.Server.Plugins`.
It lists only available providers and returns SDK manifest metadata, not server database records:

```csharp
@inject IMetadataLookup Metadata

var providers = Metadata.GetProviderNames();
var results = await Metadata.SearchGamesAsync(providerName, title, cancellationToken: cancellationToken);
if (results.Results.Count > 0)
{
    var game = await Metadata.GetGameAsync(providerName, results.Results[0].Id, cancellationToken);
}
```

A lookup does not import or change a game.
Missing or unavailable providers throw `InvalidOperationException`; provider errors propagate.
Cancellation stops waiting, but a provider without cancellation support may finish in the background.

## Reading user accounts

A plugin that lets users sign in to something it hosts with their LANCommander account, such as a
directory service or a game server, can look accounts up through
[`IUserDirectory`](/Plugins/API%20Reference#iuserdirectory):

```csharp
@inject IUserDirectory Users

var account = await Users.FindAsync(userName, cancellationToken);

if (account is { CanSignIn: true })
{
    // Check a credential the plugin keeps itself
}
```

The directory is read-only and never exposes passwords. `CanSignIn` applies the server's own sign-in
rules, lockout and approval, so a plugin that honours it refuses the same accounts the server does.

A plugin that uses LANCommander as its authentication store can check the user's LANCommander password
instead of keeping a credential of its own:

```csharp
var result = await Users.CheckPasswordAsync(userName, password, cancellationToken);

if (result.Succeeded)
{
    // Signed in as result.Account
}
else if (result.Status == PasswordCheckStatus.RateLimited)
{
    // Ask the user to try again after result.RetryAfter
}
```

Only a `Succeeded` result authenticates the user. The server's sign-in rules are applied for you:
locked-out accounts are refused (`LockedOut`) without their password being checked, unapproved ones are
refused (`NotApproved`) where the server requires approval, and nothing succeeds while password sign-in
is turned off (`Disabled`). A wrong user name and a wrong password both give `Failed`, so the result
does not reveal whether a user exists.

Failed attempts are rate limited per user name, whether or not the user exists: after five failures in
five minutes, further checks for that name return `RateLimited` without checking anything until the
oldest failure is five minutes old. A successful check clears the count. The limit is shared by every
plugin and kept in memory, so it resets when the server restarts. It does not count towards the
account's own lockout.

## Plugin settings

A plugin keeps its settings in a YAML file of its own, `Data/Settings.yaml` in the plugin's folder
(`Plugins/{Folder}/Data/Settings.yaml`), written the way the server's `Settings.yaml` is. Declare a plain class and
inject [`IPluginSettings<T>`](/Plugins/API%20Reference#ipluginsettingstsettings) with it:

```csharp
public sealed class MySettings
{
    public bool Enabled { get; set; }
    public int Port { get; set; } = 1234;
}

public sealed class MyService(IPluginSettings<MySettings> settings)
{
    public int Port => settings.Current.Port;

    public void TurnOn() => settings.Update(s => s.Enabled = true);
}
```

The server works out which plugin is asking from the assembly the settings type is declared in, so a
plugin only ever reaches its own file. `Update` saves after a short delay, so a burst of changes is
written once; call `FlushAsync` after changing something that must not be lost to a crash, such as a
rotated credential. Properties the file doesn't have take their defaults, and ones the class no
longer has are ignored. A file that can't be read gives the defaults and is left alone until the
next save.

## Storing data in the database

A plugin that needs tables of its own gets them in the server's database through a context of its
own, derived from [`PluginDbContext`](/Plugins/API%20Reference#plugindbcontext). The plugin defines
its entities; the server owns the connection, which engine is used, the migrations, and naming.

```csharp
public sealed class TimesheetContext(DbContextOptions options) : PluginDbContext(options)
{
    public DbSet<Entry> Entries => Set<Entry>();

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Entry>(entry =>
        {
            entry.Property(e => e.Name).HasMaxLength(100);
            entry.Property(e => e.Hours).HasConversion(PluginValueConverters.ScaledInteger(2));
            entry.HasIndex(e => e.Name);
        });
    }
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddPluginDbContext<TimesheetContext>();
}
```

Inject the context, or `IDbContextFactory<TimesheetContext>`, where it's needed. Never call
`Migrate()` or `EnsureCreated()`: the server applies the plugin's migrations itself, and won't hand
the context out until they're applied.

### Declaring the plugin's data

A plugin with a context declares a `DataId` and its migrations assemblies in its attribute:

```csharp
[assembly: LANCommanderPlugin(typeof(TimesheetPlugin),
    Id = "com.acme.timesheet",
    DataId = "Timesheet",
    MigrationAssemblies =
    [
        "Acme.Timesheet.Migrations.Sqlite",
        "Acme.Timesheet.Migrations.MySql",
        "Acme.Timesheet.Migrations.PostgreSql",
    ])]
```

The `DataId` names the plugin's database objects, the way the server names its own:

| Object | Name |
| --- | --- |
| Table | `Plugin_Timesheet_Entries` |
| Primary key | `PK_Plugin_Timesheet_Entries` |
| Index | `IX_Plugin_Timesheet_Entries_Name` |
| Foreign key | `FK_Plugin_Timesheet_Entries_Projects_ProjectId` |
| Migrations history | `__EFMigrationsHistory_Plugin_Timesheet` |

A name the plugin sets itself, with `HasName` or `HasDatabaseName`, is prefixed the same way.

The `DataId` is a PascalCase name of letters and digits, at most 24 characters. It must be unique
among installed plugins ignoring case, since MySQL on Windows and macOS ignores case in table names,
and it must never change once the plugin is installed, since its tables are named after it. Every
name, prefix included, has to fit in 63 characters, PostgreSQL's limit, which leaves room for table
names of 40 characters or so.

### Migrations for every engine

A LANCommander server runs on SQLite, MySQL or PostgreSQL, and the same plugin package has to install
on all of them. A plugin with a context therefore ships one migrations assembly per engine, each
declaring what it holds:

```csharp
[assembly: PluginMigrations(typeof(TimesheetContext), DatabaseEngine.MySql)]
```

Separate assemblies are needed because EF Core finds migrations by their context, so three sets in
one assembly would collide. List them as `PluginAssembly` items so they're packed into the plugin's
folder.

Each migrations project generates its migrations with a design-time factory that names things the
way the server will. Give EF the migrations assembly by name here; its design-time tooling ignores an
`Assembly`:

```csharp
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<TimesheetContext>
{
    public TimesheetContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder();

        builder.UseMySql("Server=design-time", new MySqlServerVersion(new Version(8, 0, 36)), options => options
            .MigrationsAssembly(typeof(DesignTimeFactory).Assembly.GetName().Name)
            .MigrationsHistoryTable(PluginDataIds.HistoryTable("Timesheet")));

        return new TimesheetContext(builder.UsePluginIdentity("Timesheet").Options);
    }
}
```

Then add each migration to all three, giving every one the same name:

```
dotnet ef migrations add AddProjects --project Acme.Timesheet.Migrations.Sqlite --startup-project Acme.Timesheet.Migrations.Sqlite
```

No database is needed: the factory never connects.

### What the server checks

A server only ever uses one engine, but a plugin has to work on whichever one its host chooses, so
before a plugin's startup code runs the server checks that its migrations exist for every engine:

1. There is exactly one migrations assembly per engine, holding at least one migration.
2. The engines have the same migrations, by name, in the same order.

These are read from the assemblies as they are; the server needs nothing for the engines it doesn't
use, and doesn't check that their migrations would run. The engine it does use is checked in full,
since its migrations are about to run:

3. Its migrations include a model snapshot.
4. The context's model matches the snapshot, so no migration has been forgotten.
5. The model keeps to the [portability rules](/Plugins/API%20Reference#portabilityrules): no
   engine-specific column types or SQL defaults, no schemas or sequences, converters on `decimal`,
   names of at most 63 characters, and maximum lengths of at most 191 on strings in keys and indexes.

It then applies any pending migrations to the server's database, refusing one whose database already
has migrations the plugin doesn't know (a downgrade), and checks nothing is left pending.

Whether the other engines' migrations actually work is for the plugin's own tests to show, by
applying them to a database of each kind.

A plugin that fails any check stays loaded but is turned off: its `InitializeAsync` doesn't run, and
its context throws `PluginDataUnavailableException`, carrying every error, if anything asks for it.
**Settings → Plugins** shows why, and offers a retry once the cause is fixed. Before first-time setup
has chosen a database, a plugin's migrations are checked and then applied once setup has one.

String comparison is the one difference the checks can't see: MySQL usually ignores case, while
PostgreSQL and SQLite don't. Store a lowercase copy of any column used for lookups.

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
| `Profile` | The profile menu under the signed-in user's name, alongside Change Password and Access Tokens. Shown to every signed-in user; `After` names a profile link such as `/Profile/AccessTokens` |

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

### Many entries, and groups of them

A plugin can register as many entries as it likes, one registration each. `ServerNavigationLink`
implements `IServerNavigationExtension` so they don't each need a class.

To gather entries under an expandable group, like Dashboards or Settings, register an
`IServerNavigationGroupExtension` (or a `ServerNavigationGroup`) and name its `Id` as the entries'
`Section`. A group is placed with `Section`, `After` and `Order` just like an entry, and can be nested
in another plugin group by naming that group's `Id` as its own `Section`:

```csharp
services.AddSingleton<IServerNavigationGroupExtension>(new ServerNavigationGroup
{
    Id = "com.mycompany.myplugin",
    Label = "My Plugin",
    Icon = "Package",
    Section = ServerNavigationSection.Main,
    After = ServerNavigationSection.Metadata,
});

services.AddSingleton<IServerNavigationExtension>(new ServerNavigationLink
{
    Id = "com.mycompany.myplugin.catalog",
    Label = "Catalog",
    Href = "/Plugins/MyPlugin/Catalog",
    Section = "com.mycompany.myplugin",
});

services.AddSingleton<IServerNavigationExtension>(new ServerNavigationLink
{
    Id = "com.mycompany.myplugin.reports",
    Label = "Reports",
    Href = "/Plugins/MyPlugin/Reports",
    Section = "com.mycompany.myplugin",
});
```

Inside a group, entries and nested groups are ordered by `Order`, then by label, and
`ServerNavigationAnchor.Start` places one first. A group's `Id` must not match an entry's id or a
`ServerNavigationSection` value.

Any plugin can place entries in any group. Plugins that each declare a group with the same `Id`
share a single group holding all of their entries, so each of them works with or without the others
installed. Choose an id neither plugin owns, such as `community.reports`. The shared group takes
its label, icon and placement from the declaration with the lowest `Order`, with ties settled by the
declarations' values rather than by load order, and logs a warning if the declarations disagree. It
is shown to anyone who satisfies any of the declarations' `Access`, and each entry inside it is still
checked against its own.

A group is shown only while it holds an entry the user may see. Its `Access` hides it, and everything
in it, from users who don't satisfy it; each entry inside is also checked against its own `Access`.
Groups can't be placed in `Profile`, and groups nested in a cycle are ignored with a warning. An entry
whose group isn't registered is shown in the plugin area.

### Plugin settings pages

A plugin whose only page is a handful of settings doesn't need a navigation entry. Register an
`IPluginSettingsExtension` instead, and **Settings → Plugins** offers the page as **Settings** in the
plugin's row menu, alongside its source code and project website:

```csharp
public sealed class MySettingsExtension : IPluginSettingsExtension
{
    public string Href => "/Plugins/MyPlugin/Settings";
}
```

```csharp
services.AddSingleton<IPluginSettingsExtension, MySettingsExtension>();
```

The link belongs to the plugin whose assembly holds the implementation, so there is no id to declare.
It is offered only while the plugin is running, not while it is turned off or waiting for a restart.
`Href` must be a plugin route under `/Plugins/`. A plugin registers at most one: if it registers more,
none are shown and a warning is logged. The page itself is protected like any other plugin page, and
with no `[PluginAccess]` only administrators can open it.

### Docked bars

Something that should be visible from every page, such as an application alert or a status bar, can
be docked along the top or bottom edge of the window with an `IServerDockExtension`:

```csharp
public sealed class SyncStatusDockExtension : IServerDockExtension
{
    public string Id => "com.mycompany.sync.status";
    public ServerDockPosition Position => ServerDockPosition.Bottom;
    public Type Component => typeof(SyncStatusBar);
}
```

```csharp
services.AddSingleton<IServerDockExtension, SyncStatusDockExtension>();
```

```razor
@* SyncStatusBar.razor: the bar's markup, sized to its content *@
@inject SyncStatus Status
@implements IDisposable

<div class="sync-status-bar">
    <span>@Status.Message</span>
    @if (Status.LastRun is { } lastRun)
    {
        <span>Last synced @lastRun.ToLocalTime()</span>
    }
</div>

@code {
    protected override void OnInitialized() => Status.Changed += OnChanged;

    void OnChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => Status.Changed -= OnChanged;
}
```

The bar spans the width of the window, and the navigation and the page shrink to make room for it.
It is created once when the interface loads and stays alive as the user moves between pages, so
what it holds, such as an alert the user has dismissed or its subscriptions to the plugin's
services, carries across navigation; a reload, or signing in again, starts it afresh. The component
receives no parameters, so inject the services it needs. A bar that renders no elements while it has
nothing to say, e.g. an alert banner with no active alerts, takes no space, and neither does an edge
whose bars are all empty.

Bars on the same edge are stacked by `Order`, then by `Id`, with the first against the window's
edge. Ids follow the same rules as other extension ids: letters, digits, `.`, `-` and `_`. Bars that
share an id are not shown, and neither is a bar whose `Component` isn't a Razor component; a warning
is logged for each. A bar that throws while rendering is replaced by an error in place, leaving the
other bars and the page working. With no `Access` the bar is shown only to administrators.
`Access` only decides whether the bar is drawn, so anything it reaches, such as the plugin's
endpoints, needs its own protection.

Pages that fill the window can make room for bars too: the heights of the top and bottom edges are
available as the CSS custom properties `--lc-dock-top` and `--lc-dock-bottom`, `0px` while nothing
is docked there.

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

### Import sources

The import dialog has a switcher at its top: Upload and Local File are built in, and a plugin can
add its own place to import from by registering an
[`IImportSourceExtension`](/Plugins/API%20Reference#iimportsourceextension), e.g. a store library
whose games can be imported directly. The source is offered only when importing one of its
`SupportedTypes`, and its `Component` is shown below the switcher when chosen:

```csharp
public sealed class LibraryImportSource : IImportSourceExtension
{
    public string Id => "com.mycompany.myplugin.library";
    public string Name => "My Library";
    public IReadOnlyCollection<ManifestType> SupportedTypes => [ManifestType.Game];
    public Type Component => typeof(LibraryImportPanel);
}

public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IImportSourceExtension, LibraryImportSource>();
}
```

The component takes an [`ImportSourceContext`](/Plugins/API%20Reference#importsourcecontext) as
its `Context` parameter and draws its own controls; the dialog adds Cancel and the storage location
selector. When the administrator picks something, either write an LCX package and hand it back, so
the dialog lists its records to choose from as for an upload:

```razor
@code {
    [Parameter] public ImportSourceContext Context { get; set; } = default!;

    async Task ImportAsync(LibraryGame game)
    {
        var package = await Packager.WriteAsync(game, Context.CancellationToken);

        await Context.ImportPackageAsync(package, deleteWhenDone: true);
    }
}
```

or import it yourself, through `IPackageImporter` with `Context.StorageLocationId` or the record
providers, and call `Context.CompleteAsync()` with any warnings. `Context.CancellationToken` is
cancelled when the dialog closes.

## Managing records

A server plugin can read and change the server's records through providers the server registers
alongside `IPackageImporter`. Inject them into a plugin page or another scoped plugin service.

| Provider | Manages |
| --- | --- |
| [`IGameProvider`](/Plugins/API%20Reference#igameprovider) | Games and add-ons: their fields, links to taxonomy records, redistributables and tools, external ids and custom fields |
| [`IGameVersionProvider`](/Plugins/API%20Reference#igameversionprovider) | A game's versions |
| [`ITaxonomyProvider`](/Plugins/API%20Reference#itaxonomyprovider) | Categories, collections, companies, engines, genres, platforms and tags |
| [`IActionProvider`](/Plugins/API%20Reference#iactionprovider), [`IScriptProvider`](/Plugins/API%20Reference#iscriptprovider) | Actions and scripts of games, redistributables, tools and servers |
| [`ISavePathProvider`](/Plugins/API%20Reference#isavepathprovider), [`IMultiplayerModeProvider`](/Plugins/API%20Reference#imultiplayermodeprovider), [`IKeyProvider`](/Plugins/API%20Reference#ikeyprovider) | A game's save paths, multiplayer modes and keys |
| [`IMediaProvider`](/Plugins/API%20Reference#imediaprovider) | A game's media and its files |
| [`IArchiveProvider`](/Plugins/API%20Reference#iarchiveprovider) | Lists and deletes archives. New archives arrive through `IPackageImporter` |
| [`IRedistributableProvider`](/Plugins/API%20Reference#iredistributableprovider), [`IToolProvider`](/Plugins/API%20Reference#itoolprovider), [`IServerProvider`](/Plugins/API%20Reference#iserverprovider) | Redistributables, tools, and game servers with their consoles and HTTP paths |

Records are plain `record` types. A write sets every field, so change a record by reading it and
passing a copy back:

```csharp
@inject IGameProvider Games
@inject ITaxonomyProvider Taxonomy

var game = await Games.FindByExternalIdAsync("GOG", productId)
           ?? await Games.AddAsync(new GameInfo { Title = title });

game = await Games.UpdateAsync(game with { Description = description });

var genre = await Taxonomy.GetOrAddAsync(TaxonomyKind.Genre, "Strategy");

await Games.SetLinksAsync(game.Id, GameLinkKind.Genres, [genre.Id]);
await Games.SetExternalIdAsync(game.Id, "GOG", productId);
```

An update writes a record's own fields and nothing else. Links and child records each have their
own methods, so changing one never disturbs the others; `SetLinksAsync` replaces one kind of link,
and an empty list clears it.

For bulk identity or provenance checks, `IGameProvider.GetCustomFieldsAsync()` returns every
game's custom fields with its game id. The overload taking a game id reads only that game's fields.

Use `IGameVersionProvider.GetRedistributablesAsync(versionId)` to read a particular version's
redistributables and option values, rather than relying on the game's mirrored list.
`SetRedistributablesAsync` replaces that version's links and preserves option values for links
that stay. `SetRedistributableOptionsAsync` changes the JSON options of an existing link; a missing
version or link throws `KeyNotFoundException`. The server applies its normal publishing and
mirroring rules, and new versions carry their predecessor's links and options forward.
`GetOrCreateLatestAsync(gameId)` creates an empty initial version only when necessary; it does not
create another version or publish an existing draft.

Actions, scripts, save paths and the other child records name their owner with a `RecordOwner`,
such as `RecordOwner.Game(gameId)` or `RecordOwner.Tool(toolId)`. A game's actions, scripts and
save paths belong to one of its versions: `RecordOwner.Game(gameId)` means the latest, and
`RecordOwner.Game(gameId, versionId)` a particular one.

Reads return `null` for a record that does not exist. Writes throw `KeyNotFoundException` when the
record, its owner or a record it links to does not exist, and deletes do nothing. Deleting a game,
redistributable, tool, version or media also deletes its files.

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

## Plugin data

Anything a plugin writes belongs in the `Data` folder inside its own folder, e.g.
`Plugins/LANCommander.Plugins.GOG/Data`. `PluginPaths.GetDataDirectory<MyPlugin>()` gives its path
from any type in the plugin's main assembly, so it works in `ConfigureServices`;
`PluginContext.DataDirectory` gives the same path during `InitializeAsync`. `IPluginSettings<T>`
keeps its file there. The server keeps `Data` when it installs an update over
the plugin, even one that renames the plugin's folder, and deletes it when the plugin is uninstalled.
Everything else in the plugin's folder is replaced on update, so write nowhere else in it.

`Data` may not exist yet; create it before writing.

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
