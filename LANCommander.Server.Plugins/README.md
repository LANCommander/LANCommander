# LANCommander.Server.Plugins

The contracts a LANCommander server plugin compiles against. A server plugin needs this package,
`LANCommander.SDK`, and, for pages, `LANCommander.Server.UI`. All three are loaded by the server, so
reference them with `ExcludeAssets="runtime"` to keep them out of the plugin's folder:

```xml
<PackageReference Include="LANCommander.SDK" Version="..." ExcludeAssets="runtime" />
<PackageReference Include="LANCommander.Server.Plugins" Version="..." ExcludeAssets="runtime" />
<PackageReference Include="LANCommander.Server.UI" Version="..." ExcludeAssets="runtime" />
```

Use the version of the server the plugin targets.

| Type | Use |
| --- | --- |
| `IServerRouteAssemblyExtension` | Registers the assembly holding the plugin's `@page` components |
| `IServerNavigationExtension` | Adds an entry to the server's navigation |
| `ServerNavigationSection`, `ServerNavigationAnchor` | Where in the navigation an entry is placed |
| `PluginAccessAttribute`, `PluginAccessPolicy` | Who may open a plugin page or see its entry |
| `IServerEndpointExtension` | Adds HTTP endpoints under `/api/Plugins/{RoutePrefix}` |
| `IMediaGrabberExtension`, `MediaGrabberItem` | Adds a source to the server's media search |
| `IPackageImporter` | Imports an LCX package through the server's import pipeline. Provided by the server |
| `IPackageExporter`, `PackageContents` | Describes and writes LCX packages of games, redistributables, servers and tools through the server's export pipeline. Provided by the server |
| `IServerEngineExtension`, `IServerEngineTerminal` | Runs game servers somewhere other than as a local process, e.g. in containers, with editor pages, Monitor consoles (logs, terminals) and server files of its own |
| `IRecurringJobScheduler`, `IRecurringJobHandler` | Runs plugin work on cron schedules through the server's background jobs, on its dashboard. Provided by the server |
| `IStorageLocationProvider` | Lists the server's storage locations. Provided by the server |
| `IMetadataProviderExtension`, `MetadataSubProviderInfo` | Adds a source of game metadata alongside HQ, IGDB and PCGamingWiki |
| `IMetadataLookup` | Searches configured metadata providers and fetches full game metadata without importing it. Provided by the server |
| `IUserDirectory`, `UserAccount` | Reads user accounts, their roles and whether they may sign in, without passwords. Provided by the server |
| `Data.PluginDbContext`, `AddPluginDbContext<T>` | A plugin's own tables in the server's database, named and migrated by the server |
| `Data.PluginMigrationsAttribute`, `Data.DatabaseEngine` | Declares a migrations assembly; a plugin ships one per engine (SQLite, MySQL, PostgreSQL) |
| `Data.PluginValueConverters`, `Data.PortabilityRules` | Keeps a plugin model behaving the same on every engine |
| `IGameProvider`, `IGameVersionProvider` | Manage games and add-ons, their links, external ids, custom fields and versions. Provided by the server |
| `IActionProvider`, `IScriptProvider`, `ISavePathProvider`, `IMultiplayerModeProvider`, `IKeyProvider` | Manage the records that belong to a game, redistributable, tool or server (`RecordOwner`). Provided by the server |
| `IMediaProvider`, `IArchiveProvider` | Manage a game's media files; list and delete archives. Provided by the server |
| `ITaxonomyProvider` | Manage categories, collections, companies, engines, genres, platforms and tags. Provided by the server |
| `IRedistributableProvider`, `IToolProvider`, `IServerProvider` | Manage redistributables, tools, and game servers with their consoles and HTTP paths; list, read and write server files through their selected engine. Provided by the server |
| `IMatchStatisticsSink` | Records multiplayer matches, their events, results, statistics and attachments as a trusted reporter, and links in-game ids to users. Provided by the server |
| `IMatchStatisticsProvider` | Reads recorded matches, their events and attachments, and aggregates a statistic across matches (leaderboards). Provided by the server |
| `GameCreatedEvent`, `ArchiveUploadedEvent`, `UserCreatedEvent`, `PlaySessionStartedEvent`, `IssueCreatedEvent`, `ServerStatusChangedEvent`, `MatchEndedEvent`, `MatchEventsRecordedEvent`, ... | Server events, raised through the SDK's `IPluginEventBus` |

A plugin with a context needs `DataId` and `MigrationAssemblies` in its `[LANCommanderPlugin]`
attribute. Plugin settings, `IPluginSettings<T>`, are in `LANCommander.SDK`.

Subscribe to server events in `InitializeAsync` and dispose the subscription when the plugin stops:

```csharp
var events = context.Services.GetRequiredService<IPluginEventBus>();

_subscription = events.Subscribe<PlaySessionStartedEvent>((e, ct) =>
{
    // Runs on the request that started the session: hand slow work off
    return Task.CompletedTask;
});
```

The events are games created, updated and deleted; archives uploaded; user accounts created,
approved and deleted; play sessions started and ended; issues reported and resolved; and game
server status changes. Handlers of all but the last run on the request that caused the change and
are awaited, so keep them quick. A handler that throws is logged and changes nothing else.

The [documentation site](https://docs.lancommander.app/) has a walkthrough under Plugins.

`IGameProvider.GetCustomFieldsAsync()` reads custom fields across all games, including their game
ids, for bulk provenance checks. `IGameVersionProvider` can read and replace a version's
redistributables and set their per-version option values; keeping a redistributable keeps its
options, and creating a new version carries those options forward. `GetOrCreateLatestAsync`
creates an empty initial version only when the game has none, without publishing an existing draft.

`IServerProvider.ListFilesAsync(serverId, relativePath, cancellationToken)`,
`OpenFileAsync(serverId, relativePath, cancellationToken)` and
`WriteFileAsync(serverId, relativePath, content, cancellationToken)` use the host's checked file
routing, not a plugin's engine implementation or credentials. An empty list path means the engine's
file root: a local working directory or the root configured by an extension (not necessarily its
executable directory). Paths must be relative; traversal, absolute paths and malformed names throw.
Listing returns `ServerFileInfo` with `Name`, root-relative `Path` (using `/`), `IsDirectory`,
nullable `Size` in bytes and nullable UTC `LastModified`.

Missing servers throw `KeyNotFoundException`, missing engines throw `InvalidOperationException`,
and engines without file support throw `NotSupportedException`. Engine failures propagate, including
unavailable or stopped containers; they are not empty inventories. Missing folders and files remain
`DirectoryNotFoundException` and `FileNotFoundException` respectively. The engine is responsible for
reporting availability; local files do not require a running game process.

Dispose an opened stream before the plugin service scope ends. It may be nonseekable; use
cancellable reads rather than assuming `Length` or `Position` are available. Writes read from the
content stream's current position and leave it open. Writes replace existing content, create parent
folders and are not atomic; cancellation can leave partial content. These trusted host services do
not authorize callers: protect plugin pages/endpoints with `PluginAccess`/endpoint access policies
and use a host-created scope for background work.

These file-provider methods are an unreleased addition, not part of published `2.2.0-beta.1`.
For source validation, pack the SDK, server plugin contracts and server UI with the same explicit
local version (for example `-p:Version=2.2.0-beta.1-fileaccess.local.1`) into a private local feed.
Reference that version only for the consumer under development, keeping `ExcludeAssets="runtime"`.
An actual host built from the matching source is required; installing compile-time packages does
not add the implementation to an existing beta.1 host. Publication and release integration are
separate prerequisites.
