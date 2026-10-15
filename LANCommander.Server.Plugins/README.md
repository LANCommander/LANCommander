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
| `PluginAccessAttribute`, `PluginAccessPolicy` | Who may open a plugin page or see its entry |
| `IPackageImporter` | Imports an LCX package through the server's import pipeline. Provided by the server |
| `IStorageLocationProvider` | Lists the server's storage locations. Provided by the server |

The [documentation site](https://docs.lancommander.app/) has a walkthrough under Plugins.
