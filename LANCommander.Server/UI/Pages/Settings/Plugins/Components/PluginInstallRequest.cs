using LANCommander.PluginManagement;

namespace LANCommander.Server.UI.Pages.Settings.Plugins.Components;

/// <summary>A package to install: on the server, or, for a launcher plugin, into the server's registry for launchers.</summary>
public sealed record PluginInstallRequest(PluginPackageListing Listing, bool ForLaunchers = false);
