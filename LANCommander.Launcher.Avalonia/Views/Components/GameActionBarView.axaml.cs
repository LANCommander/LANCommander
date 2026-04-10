using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Threading;
using LANCommander.Launcher.Avalonia.ViewModels.Components;

namespace LANCommander.Launcher.Avalonia.Views.Components;

public partial class GameActionBarView : UserControl
{
    private GameActionBarViewModel? _vm;
    private int _injectedItemCount;

    public GameActionBarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private MenuFlyout? PlayFlyout => PlaySplitButton.Flyout as MenuFlyout;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm != null)
            _vm.SecondaryActions.CollectionChanged -= OnSecondaryActionsChanged;

        _vm = DataContext as GameActionBarViewModel;

        if (_vm != null)
            _vm.SecondaryActions.CollectionChanged += OnSecondaryActionsChanged;

        RefreshSecondaryItems();
    }

    private void OnSecondaryActionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => Dispatcher.UIThread.Post(RefreshSecondaryItems);

    private void RefreshSecondaryItems()
    {
        var flyout = PlayFlyout;
        if (flyout is null || _vm is null) return;

        // Remove previously injected items (always at the front of the flyout)
        for (var i = 0; i < _injectedItemCount; i++)
            flyout.Items.RemoveAt(0);
        _injectedItemCount = 0;

        if (_vm.SecondaryActions.Count == 0) return;

        var idx = 0;
        foreach (var action in _vm.SecondaryActions)
        {
            flyout.Items.Insert(idx++, new MenuItem { Header = action.Name, Command = action.RunCommand });
            _injectedItemCount++;
        }

        // Separator between secondary actions and static items
        flyout.Items.Insert(idx, new Separator());
        _injectedItemCount++;
    }
}
