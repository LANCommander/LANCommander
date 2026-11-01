#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using LANCommander.Launcher.Services.Plugins;
using LANCommander.Launcher.Settings;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;
using LANCommander.PluginManagement;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The plugins page, reached from settings.</summary>
public static class PluginFixtures
{
    private static readonly LauncherPluginPackage ServerOverlay = new()
    {
        Id = "Acme.Overlay",
        Version = "1.2.0",
        Title = "Acme Overlay",
        Description = "Shows who's online over every game.",
        Authors = "Acme",
        Required = true,
    };

    private static readonly LauncherPluginPackage ServerThemes = new()
    {
        Id = "LANCommander.Plugins.Themes",
        Version = "2.2.0",
        Title = "Themes",
        Description = "Extra colour schemes for the launcher.",
        Authors = "LANCommander",
        IsOfficial = true,
    };

    private static IEnumerable<InstalledPluginItem> Installed =>
    [
        new(new InstalledPlugin
        {
            Folder = "Acme.Overlay",
            Name = "Acme Overlay",
            Version = "1.1.0",
            State = PluginInstallState.Loaded,
            Package = new PluginPackageRecord { Id = "Acme.Overlay", Version = "1.1.0", SourceName = LauncherPluginService.ServerSourceName, SourceUrl = LauncherPluginService.ServerSourceUrl },
        }, isRequired: true, new PluginUpdateOffer("1.2.0", ServerOverlay, null)),

        new(new InstalledPlugin
        {
            Folder = "Contoso.Stats",
            Name = "Play Stats",
            Version = "0.4.0",
            State = PluginInstallState.InstallPending,
            PendingPackage = new PluginPackageRecord { Id = "Contoso.Stats", Version = "0.4.0", SourceName = PluginFeed.NuGetOrgName, SourceUrl = PluginFeed.NuGetOrgUrl },
        }, isRequired: false, null),

        new(new InstalledPlugin
        {
            Folder = "HomeBrew.Tweaks",
            Name = "HomeBrew Tweaks",
            Version = "1.0.0",
            State = PluginInstallState.NotLoaded,
            Package = new PluginPackageRecord { Id = "HomeBrew.Tweaks", Version = "1.0.0" },
        }, isRequired: false, null),
    ];

    private static IEnumerable<PluginListingItem> Results =>
    [
        PluginListingItem.FromServer(ServerOverlay, "1.1.0"),
        PluginListingItem.FromServer(ServerThemes, null),
    ];

    // After the packages it shows, which static initialisers must reach first
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        Plugins("Plugins", "Plugins installed from the server, a registry and a file, with the server's other plugins to browse",
            Installed, Results, [], restartRequired: true),

        Plugins("Plugins.Empty", "No plugins installed, and nothing on the server", [], [], []),

        Plugins("Plugins.Registries", "The registries list scrolled into view, with a feed of the player's own",
            Installed, Results,
            [new PluginPackageSource { Name = "Studio Feed", Url = "https://pkgs.example.com/lancommander/v3/index.json", Username = "studio" }],
            prepare: window => window.GetVisualDescendants().OfType<PluginsView>().Single()
                .GetVisualDescendants().OfType<ScrollViewer>().First()
                .ScrollToEnd()),
    ];

    private static ViewFixture Plugins(
        string name,
        string description,
        IEnumerable<InstalledPluginItem> installed,
        IEnumerable<PluginListingItem> results,
        IEnumerable<PluginPackageSource> sources,
        bool restartRequired = false,
        Action<Window>? prepare = null) =>
        new(name, description, context =>
        {
            var plugins = new PluginsViewModel(context.Services);

            plugins.SeedFixture(installed, results, sources, restartRequired);

            return context.ShellWindow(plugins);
        })
        {
            Prepare = prepare == null ? null : (_, window) => prepare(window),
        };
}
#endif
