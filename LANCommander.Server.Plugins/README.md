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
| `IMetadataLookup` | Searches configured metadata providers and fetches full game metadata without importing it. Provided by the server |
| `IUserDirectory`, `UserAccount` | Reads user accounts, their roles and whether they may sign in, without passwords. Provided by the server |
| `Data.PluginDbContext`, `AddPluginDbContext<T>` | A plugin's own tables in the server's database, named and migrated by the server |
| `Data.PluginMigrationsAttribute`, `Data.DatabaseEngine` | Declares a migrations assembly; a plugin ships one per engine (SQLite, MySQL, PostgreSQL) |
| `Data.PluginValueConverters`, `Data.PortabilityRules` | Keeps a plugin model behaving the same on every engine |

A plugin with a context needs `DataId` and `MigrationAssemblies` in its `[LANCommanderPlugin]`
attribute. Plugin settings, `IPluginSettings<T>`, are in `LANCommander.SDK`.
| `IGameProvider`, `IGameVersionProvider` | Manage games and add-ons, their links, external ids, custom fields and versions. Provided by the server |
| `IActionProvider`, `IScriptProvider`, `ISavePathProvider`, `IMultiplayerModeProvider`, `IKeyProvider` | Manage the records that belong to a game, redistributable, tool or server (`RecordOwner`). Provided by the server |
| `IMediaProvider`, `IArchiveProvider` | Manage a game's media files; list and delete archives. Provided by the server |
| `ITaxonomyProvider` | Manage categories, collections, companies, engines, genres, platforms and tags. Provided by the server |
| `IRedistributableProvider`, `IToolProvider`, `IServerProvider` | Manage redistributables, tools, and game servers with their consoles and HTTP paths. Provided by the server |

The [documentation site](https://docs.lancommander.app/) has a walkthrough under Plugins.

`IGameProvider.GetCustomFieldsAsync()` reads custom fields across all games, including their game
ids, for bulk provenance checks. `IGameVersionProvider` can read and replace a version's
redistributables and set their per-version option values; keeping a redistributable keeps its
options, and creating a new version carries those options forward. `GetOrCreateLatestAsync`
creates an empty initial version only when the game has none, without publishing an existing draft.
