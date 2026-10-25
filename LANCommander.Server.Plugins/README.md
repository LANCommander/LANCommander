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
| `IMediaGrabberExtension`, `MediaGrabberItem` | Adds a source to the server's media search |
| `IPackageImporter` | Imports an LCX package through the server's import pipeline. Provided by the server |
| `IStorageLocationProvider` | Lists the server's storage locations. Provided by the server |
| `IGameProvider`, `IGameVersionProvider` | Manage games and add-ons, their links, external ids, custom fields and versions. Provided by the server |
| `IActionProvider`, `IScriptProvider`, `ISavePathProvider`, `IMultiplayerModeProvider`, `IKeyProvider` | Manage the records that belong to a game, redistributable, tool or server (`RecordOwner`). Provided by the server |
| `IMediaProvider`, `IArchiveProvider` | Manage a game's media files; list and delete archives. Provided by the server |
| `ITaxonomyProvider` | Manage categories, collections, companies, engines, genres, platforms and tags. Provided by the server |
| `IRedistributableProvider`, `IToolProvider`, `IServerProvider` | Manage redistributables, tools, and game servers with their consoles and HTTP paths. Provided by the server |

The [documentation site](https://docs.lancommander.app/) has a walkthrough under Plugins.
