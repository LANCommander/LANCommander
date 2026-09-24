#if DEBUG
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// Debug-only fixture for the Install dialog. Set <c>LANCOMMANDER_FAKE_INSTALL_OPTIONS=1</c> before
/// starting a Debug build, then click Install on any game (Battlefield 1942 is the intended one):
/// the dialog opens with Battlefield 1942's expansions, a long list of mods and a few tools, so the
/// layout and scrolling can be checked without the server having add-ons. Nothing is imported,
/// downloaded or installed, whichever button closes the dialog.
/// </summary>
public partial class GameActionBarViewModel
{
    public const string InstallOptionsFixtureVariable = "LANCOMMANDER_FAKE_INSTALL_OPTIONS";

    public static bool IsInstallOptionsFixtureRequested =>
        Environment.GetEnvironmentVariable(InstallOptionsFixtureVariable) is "1" or "true";

    internal const long MB = 1024L * 1024;

    internal static readonly (string Title, GameType Type, long DownloadMb, bool Selected)[] FixtureAddons =
    [
        ("Battlefield 1942: The Road to Rome",            GameType.Expansion, 412,  true),
        ("Battlefield 1942: Secret Weapons of WWII",      GameType.Expansion, 538,  true),
        ("Desert Combat",                                 GameType.Mod,       1_230, false),
        ("Desert Combat Final",                           GameType.Mod,       1_410, false),
        ("Forgotten Hope",                                GameType.Mod,       2_050, false),
        ("Galactic Conquest",                             GameType.Mod,       780,  false),
        ("Eve of Destruction",                            GameType.Mod,       960,  false),
        ("Battlegroup42",                                 GameType.Mod,       1_120, false),
        ("Battlefield Pirates",                           GameType.Mod,       310,  false),
        ("Battlefield Pirates 2",                         GameType.Mod,       655,  false),
        ("Siegecraft: Medieval warfare with a very long mod name to check trimming", GameType.Mod, 420, false),
        ("Experience WW2",                                GameType.Mod,       540,  false),
        ("Operation Market Garden",                       GameType.Mod,       720,  false),
        ("Interstate '82 Conversion",                     GameType.Mod,       95,   false),
    ];

    internal static readonly (string Name, long DownloadMb)[] FixtureTools =
    [
        ("BF1942 Dedicated Server",  164),
        ("BF Remote Console",        3),
        ("Mod Development Toolkit",  88),
    ];

    private async Task ShowInstallOptionsFixtureAsync()
    {
        _logger.LogWarning("Install options fixture active ({Variable}); nothing will be installed", InstallOptionsFixtureVariable);

        using var scope = _serviceProvider.CreateScope();
        var settingsProvider = scope.ServiceProvider.GetRequiredService<ISettingsProvider>();

        var optionsVm = new InstallOptionsViewModel
        {
            GameTitle = Title ?? "Battlefield 1942",
            ConfirmButtonText = "Install",
            BaseDownloadSize = 1_180 * MB,
            BaseSpaceRequired = 1_650 * MB,
        };
        optionsVm.DialogTitle = $"Install {optionsVm.GameTitle}";

        // The real folders, plus the system drive so the picker always has a choice to show.
        foreach (var dir in (settingsProvider.CurrentValue.Games.InstallDirectories ?? []).Append(@"C:\Games").Distinct())
            optionsVm.InstallDirectories.Add(dir);

        optionsVm.SelectedInstallDirectory = optionsVm.InstallDirectories.First();

        foreach (var addon in FixtureAddons)
        {
            optionsVm.Addons.Add(new InstallAddonItemViewModel(new SDK.Models.Game
            {
                Id = Guid.NewGuid(),
                Title = addon.Title,
                Type = addon.Type,
                Archives = [new SDK.Models.Archive { CompressedSize = addon.DownloadMb * MB, UncompressedSize = addon.DownloadMb * MB * 3 / 2 }],
            }, addon.Selected));
        }

        foreach (var tool in FixtureTools)
        {
            optionsVm.Tools.Add(new InstallToolItemViewModel(new SDK.Models.Tool
            {
                Id = Guid.NewGuid(),
                Name = tool.Name,
                Archives = [new SDK.Models.Archive { CompressedSize = tool.DownloadMb * MB, UncompressedSize = tool.DownloadMb * MB * 2 }],
            }));
        }

        var tcs = new TaskCompletionSource<bool?>();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new Views.InstallOptionsOverlay
            {
                DataContext = optionsVm,
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.DialogClosed += (_, result) => tcs.TrySetResult(result);

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var layer = OverlayLayer.GetOverlayLayer(mainWindow);

            if (layer is null)
            {
                tcs.TrySetResult(null);
                return;
            }

            overlay.Bind(global::Avalonia.Layout.Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
            overlay.Bind(global::Avalonia.Layout.Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });
            layer.Children.Add(overlay);
        });

        var confirmed = await tcs.Task;

        _logger.LogInformation(
            "Install options fixture closed (confirmed: {Confirmed}); would install to {Directory} with {Addons} add-on(s) and {Tools} tool(s)",
            confirmed, optionsVm.SelectedInstallDirectory, optionsVm.SelectedAddons.Length, optionsVm.SelectedTools.Length);
    }
}
#endif
