using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.ViewModels.ScriptDebugger;

public sealed partial class BreakpointsViewModel : ObservableObject
{
    private readonly BreakpointStore _store;

    public BreakpointsViewModel(BreakpointStore store) => _store = store;

    public ObservableCollection<BreakpointModel> Items => _store.Items;

    [ObservableProperty]
    private BreakpointModel? _selected;

    /// <summary>Raised when the user double-clicks a breakpoint, so the editor can navigate.</summary>
    public event Action<int>? NavigateRequested;

    [RelayCommand]
    private void Remove(BreakpointModel? model)
    {
        if (model is not null)
            _store.Remove(model);
    }

    [RelayCommand]
    private void RemoveAll() => _store.Clear();

    [RelayCommand]
    private void Navigate(BreakpointModel? model)
    {
        if (model is not null && model.Line > 0)
            NavigateRequested?.Invoke(model.Line);
    }
}
