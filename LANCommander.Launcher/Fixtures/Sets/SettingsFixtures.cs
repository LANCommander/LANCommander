#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>The settings page.</summary>
public static class SettingsFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        Settings("Settings", "Settings as loaded", _ => { }),

        Settings("Settings.Bottom", "Settings scrolled to the update and debug options, with an update available", settings =>
            {
                settings.IsUpdateSupported = true;
                settings.IsUpdateAvailable = true;
                settings.UpdateStatus = "Version 2.4.0 is available.";
            },
            prepare: window => window.GetVisualDescendants().OfType<SettingsView>().Single()
                .GetVisualDescendants().OfType<ScrollViewer>().First()
                .ScrollToEnd()),

        Settings("Settings.Saved", "Settings just after saving", settings =>
            settings.StatusMessage = "Settings saved!"),

        Settings("Settings.Saving", "Settings while saving", settings =>
        {
            settings.IsSaving = true;
            settings.StatusMessage = "Saving...";
        }),
    ];

    private static ViewFixture Settings(string name, string description, Action<SettingsViewModel> configure, Action<Window>? prepare = null) =>
        new(name, description, context =>
        {
            var settings = context.Shell.SettingsViewModel;

            settings.Load();

            // The default is under the user's own data folder.
            settings.MediaStoragePath = @"D:\LANCommander\Media";

            // The real version changes with every build
            settings.CurrentVersion = "2.3.0";

            configure(settings);

            return context.ShellWindow(settings);
        })
        {
            Prepare = prepare == null ? null : (_, window) => prepare(window),
        };
}
#endif
