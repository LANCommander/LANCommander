using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LANCommander.Launcher.ViewModels;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.Controls;

/// <summary>
/// Builds the consolidated, state-driven game menu and exposes it both as a flyout for the
/// action bar split buttons and as a right-click / gamepad context menu on game covers and
/// list rows.
///
/// The menu items bind against a <see cref="GameActionBarViewModel"/> via an explicit
/// <see cref="Binding.Source"/>, so the same definition works regardless of the host control's
/// own DataContext (a <see cref="GameItemViewModel"/> in grids and lists).
///
/// Attached-behavior usage: <c>controls:GameContextMenu.IsEnabled="True"</c> on a focusable
/// game cover or list row.
/// </summary>
public static class GameContextMenu
{
    private static readonly IValueConverter Invert = new FuncValueConverter<bool, bool>(b => !b);

    // Keeps each flyout's secondary-action subscription alive for the flyout's lifetime and
    // allows callers to detach it explicitly when the flyout is discarded.
    private static readonly ConditionalWeakTable<MenuFlyout, SecondaryActionsBinder> Binders = new();

    // ── Attached behavior ───────────────────────────────────────────────────────────────────

    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(GameContextMenu));

    static GameContextMenu()
    {
        IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
    }

    public static bool GetIsEnabled(Control control)
        => control.GetValue(IsEnabledProperty);
    
    public static void SetIsEnabled(Control control, bool value)
        => control.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.ContextRequested -= OnContextRequested;
        control.KeyDown -= OnKeyDown;

        if (e.NewValue is true)
        {
            control.ContextRequested += OnContextRequested;
            control.KeyDown += OnKeyDown;
        }
    }

    private static void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is Control control && OpenMenu(control, atPointer: true))
            e.Handled = true;
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // The gamepad service maps the North (Y) button to Key.Apps on the focused element.
        if (e.Key == Key.Apps && sender is Control control && OpenMenu(control, atPointer: false))
            e.Handled = true;
    }

    private static bool OpenMenu(Control control, bool atPointer)
    {
        if (control.DataContext is not GameItemViewModel item)
            return false;
        
        if (App.Services is not { } serviceProvider)
            return false;

        var vm = new GameActionBarViewModel(serviceProvider);

        // Load asynchronously, then show. Disposed on close so no timers leak (LoadForMenuAsync
        // doesn't start any, but the VM owns disposable timers in other code paths).
        _ = ShowAsync();
        
        return true;

        async System.Threading.Tasks.Task ShowAsync()
        {
            try
            {
                await vm.LoadForMenuAsync(item);
            }
            catch
            {
                vm.Dispose();
                return;
            }

            var flyout = CreateFlyout(vm);

            void OnLibraryChanged(object? s, System.EventArgs e) => RefreshHost(control);

            void OnClosed(object? s, System.EventArgs e)
            {
                flyout.Closed -= OnClosed;
                DetachBinder(flyout);

                // Don't unsubscribe LibraryChanged/InstallRequested here: clicking a menu item
                // closes the flyout immediately, but the backing command (e.g. Remove from
                // Library) runs asynchronously and only raises these events after its await. We
                // must stay subscribed so the host grid/list still refreshes once it completes.
                // Disposing is safe (it only stops running-check timers, which the menu path
                // never starts) and the VM is collected once the in-flight command finishes.
                vm.Dispose();
            }

            vm.LibraryChanged += OnLibraryChanged;
            vm.InstallRequested += OnLibraryChanged;
            flyout.Closed += OnClosed;

            flyout.ShowAt(control, atPointer);
        }
    }

    /// <summary>
    /// Refreshes the nearest ancestor games collection so library/install changes made from the
    /// menu (add/remove, install, uninstall) are reflected in the grid or list immediately.
    /// </summary>
    private static void RefreshHost(Control control)
    {
        Visual? current = control;
        
        while (current != null)
        {
            if (current is StyledElement { DataContext: GamesCollectionViewModel collection })
            {
                _ = collection.LoadGamesAsync();
                return;
            }
            
            current = current.GetVisualParent();
        }
    }

    // ── Flyout factory ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a consolidated game menu bound to the given view model. The caller owns the
    /// returned flyout; call <see cref="DetachBinder"/> when it is discarded.
    /// </summary>
    public static MenuFlyout CreateFlyout(GameActionBarViewModel vm)
    {
        var flyout = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedLeft };

        Populate(flyout, vm);
        Binders.AddOrUpdate(flyout, new SecondaryActionsBinder(flyout, vm));

        return flyout;
    }

    /// <summary>
    /// Stops keeping the flyout's secondary-action subscription so the flyout and its view model
    /// can be collected.
    /// </summary>
    public static void DetachBinder(MenuFlyout flyout)
    {
        if (Binders.TryGetValue(flyout, out var binder))
        {
            binder.Detach();
            Binders.Remove(flyout);
        }
    }

    private static void Populate(MenuFlyout flyout, GameActionBarViewModel vm)
    {
        var items = new List<Control>
        {
            // Primary action (state-driven)
            Item("Install", vm, "InstallCommand", visiblePath: "IsInstalled", visibleInvert: true, enabledPath: "CanInstall"),
            Item("Play", vm, "PlayCommand", visiblePath: "IsInstalled"),
            Item("Update", vm, "UpdateGameCommand", visiblePath: "ShowUpdateLabel"),
            Item("Play Without Updating", vm, "PlayOrStopCommand", visiblePath: "IsUpdateAvailable"),
        };

        // Dynamic, game-defined secondary actions
        foreach (var action in vm.SecondaryActions)
            items.Add(new MenuItem { Header = action.Name, Command = action.RunCommand });

        items.Add(Separator(vm, "IsInstalled"));
        items.Add(Item("Browse Files", vm, "BrowseFilesCommand", visiblePath: "IsInstalled"));
        items.Add(Item("View Manual", vm, "OpenFirstManualCommand", visiblePath: "HasManuals"));
        items.Add(Item("Modify", vm, "ModifyCommand", visiblePath: "IsInstalled"));
        items.Add(Item("Select Version...", vm, "SelectVersionCommand", visiblePath: "IsInstalled"));
        items.Add(Separator(vm, "IsInstalled"));
        items.Add(Item("Verify Files", vm, "VerifyFilesCommand", visiblePath: "IsInstalled", enabledPath: "IsVerifyingFiles", enabledInvert: true));
        items.Add(Item("Uninstall", vm, "UninstallCommand", visiblePath: "IsInstalled", enabledPath: "IsUninstalling", enabledInvert: true));
        items.Add(Item("Add to Library", vm, "AddToLibraryCommand", visiblePath: "IsInLibrary", visibleInvert: true));
        items.Add(Item("Remove from Library", vm, "RemoveFromLibraryCommand", enabledPath: "IsInLibrary"));

        flyout.Items.Clear();
        
        foreach (var item in items)
            flyout.Items.Add(item);
    }

    private static MenuItem Item(
        string header,
        GameActionBarViewModel vm,
        string commandPath,
        string? visiblePath = null,
        bool visibleInvert = false,
        string? enabledPath = null,
        bool enabledInvert = false)
    {
        var item = new MenuItem { Header = header };

        item.Bind(MenuItem.CommandProperty, new Binding(commandPath) { Source = vm });

        if (visiblePath != null)
            item.Bind(Visual.IsVisibleProperty, new Binding(visiblePath)
            {
                Source = vm,
                Converter = visibleInvert ? Invert : null,
            });

        if (enabledPath != null)
            item.Bind(InputElement.IsEnabledProperty, new Binding(enabledPath)
            {
                Source = vm,
                Converter = enabledInvert ? Invert : null,
            });

        return item;
    }

    private static Separator Separator(GameActionBarViewModel vm, string visiblePath)
    {
        var separator = new Separator();
        
        separator.Bind(Visual.IsVisibleProperty, new Binding(visiblePath) { Source = vm });
        
        return separator;
    }

    /// <summary>
    /// Keeps the dynamic secondary actions in the flyout in sync with the view model, which may
    /// populate them asynchronously after the flyout is created.
    /// </summary>
    private sealed class SecondaryActionsBinder
    {
        private readonly MenuFlyout _flyout;
        private readonly GameActionBarViewModel _vm;

        public SecondaryActionsBinder(MenuFlyout flyout, GameActionBarViewModel vm)
        {
            _flyout = flyout;
            _vm = vm;
            _vm.SecondaryActions.CollectionChanged += OnChanged;
        }

        private void OnChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => Dispatcher.UIThread.Post(() => Populate(_flyout, _vm));

        public void Detach()
            => _vm.SecondaryActions.CollectionChanged -= OnChanged;
    }
}
