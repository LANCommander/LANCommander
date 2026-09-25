using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.ScriptDebugger.Items;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

/// <summary>User-supplied expressions, re-evaluated at every stop.</summary>
public sealed partial class WatchViewModel : ObservableObject
{
    private readonly DebugSessionController _controller;

    public WatchViewModel(DebugSessionController controller) => _controller = controller;

    public ObservableCollection<WatchItemViewModel> Items { get; } = new();

    [ObservableProperty]
    private string _newExpression = string.Empty;

    [ObservableProperty]
    private WatchItemViewModel? _selected;

    [RelayCommand]
    private async Task AddAsync()
    {
        var expression = NewExpression.Trim();
        if (expression.Length == 0)
            return;

        NewExpression = string.Empty;

        var item = new WatchItemViewModel(expression, _controller);
        Items.Add(item);

        await item.RefreshAsync();
    }

    [RelayCommand]
    private void Remove(WatchItemViewModel? item)
    {
        if (item is not null)
            Items.Remove(item);
    }

    /// <summary>
    /// Re-evaluate everything after a stop. Watches run concurrently: each is an independent
    /// round trip through the pump, and serialising them would make a panel of five watches feel
    /// five times slower than it needs to.
    /// </summary>
    public Task RefreshAllAsync() => Task.WhenAll(Items.Select(item => item.RefreshAsync()));

    public void MarkUnavailable()
    {
        foreach (var item in Items)
            item.MarkUnavailable();
    }
}
