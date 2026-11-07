using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.ViewModels.Components;
using LANCommander.SDK.Enums;
using ManifestAction = LANCommander.SDK.Models.Manifest.Action;

namespace LANCommander.Launcher.Views;

public partial class GameActionsOverlay : UserControl
{
    public event EventHandler<ManifestAction?>? ActionSelected;

    public GameActionsOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, () => Close(null));
    }

    /// <summary>
    /// The action to play <paramref name="gameTitle"/> with. With more than one primary action the
    /// player picks one; with a single one it's that, else the first secondary. Cancelled is true when
    /// the player closed the chooser; Action is null then, or when nothing can play the game.
    /// </summary>
    /// <param name="choices">The actions to offer, if the caller already has them labelled; else
    /// <paramref name="actions"/>' primary actions are.</param>
    public static async Task<(ManifestAction? Action, bool Cancelled)> ChoosePlayActionAsync(
        string gameTitle,
        IEnumerable<ManifestAction> actions,
        IEnumerable<GameActionViewModel>? choices = null)
    {
        var list = actions?.ToList() ?? new List<ManifestAction>();

        var offered = choices?.ToList()
            ?? list.Where(a => a.Type == ActionType.Primary)
                .Select(a => new GameActionViewModel(a, _ => Task.CompletedTask))
                .ToList();

        if (offered.Count <= 1)
            return (offered.FirstOrDefault()?.Action ?? ActionTypes.SelectDefault(list), false);

        var chosen = await ShowAsync(gameTitle, offered);

        return (chosen, chosen == null);
    }

    /// <summary>Asks the player to pick one of <paramref name="actions"/>; null when they cancel.</summary>
    public static async Task<ManifestAction?> ShowAsync(string gameTitle, IEnumerable<GameActionViewModel> actions)
    {
        var tcs = new TaskCompletionSource<ManifestAction?>();
        var offered = actions.ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new GameActionsOverlay
            {
                DataContext = new GameActionsOverlayViewModel(gameTitle, offered),
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.ActionSelected += (_, action) => tcs.TrySetResult(action);

            var mainWindow = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            // Started from the tray, the window may be hidden; the player has to see the choice
            if (mainWindow is MainWindow { IsVisible: false } window)
                window.RestoreFromTray();

            var layer = OverlayLayer.GetOverlayLayer(mainWindow);

            if (layer is not null)
            {
                overlay.Bind(WidthProperty, new Binding("Bounds.Width") { Source = layer });
                overlay.Bind(HeightProperty, new Binding("Bounds.Height") { Source = layer });
                layer.Children.Add(overlay);
            }
            else
            {
                // Nowhere to ask; play the first, as a single primary action would
                tcs.TrySetResult(offered.First().Action);
            }
        });

        return await tcs.Task;
    }

    private void Action_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GameActionViewModel vm })
            Close(vm.Action);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close(null);

    private void Close(ManifestAction? action)
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        ActionSelected?.Invoke(this, action);
        layer?.Children.Remove(this);
    }
}
