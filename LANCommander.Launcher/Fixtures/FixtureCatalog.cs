#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.Launcher.Fixtures.Sets;

namespace LANCommander.Launcher.Fixtures;

/// <summary>Every fixture, in the order the preview gallery lists them.</summary>
public static class FixtureCatalog
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        .. StartupFixtures.All,
        .. LibraryFixtures.All,
        .. ShellFixtures.All,
        .. DepotFixtures.All,
        .. GameDetailFixtures.All,
        .. OverlayFixtures.All,
        .. DownloadFixtures.All,
        .. VerifyFixtures.All,
        .. SettingsFixtures.All,
        .. PackagingFixtures.All,
        .. WindowFixtures.All,
        .. ComponentFixtures.All,
    ];

    public static ViewFixture? Find(string name) =>
        All.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static ViewFixture Get(string name) =>
        Find(name) ?? throw new KeyNotFoundException($"No fixture named '{name}'.");
}
#endif
