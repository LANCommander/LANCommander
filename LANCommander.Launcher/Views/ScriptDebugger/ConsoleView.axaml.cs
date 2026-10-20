using System;
using System.Collections.Specialized;
using Avalonia.Controls;
using LANCommander.Launcher.ViewModels.ScriptDebugger;

namespace LANCommander.Launcher.Views.ScriptDebugger;

public partial class ConsoleView : UserControl
{
    private INotifyCollectionChanged? _lines;

    public ConsoleView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_lines is not null)
            _lines.CollectionChanged -= OnLinesChanged;

        _lines = (DataContext as ConsoleViewModel)?.Lines;

        if (_lines is not null)
            _lines.CollectionChanged += OnLinesChanged;
    }

    /// <summary>
    /// Follow the tail. The view model already batches writes on a timer, so this fires at most 20 times
    /// a second no matter how much the script produces.
    /// </summary>
    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is not (NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Reset))
            return;

        if (Output.ItemCount > 0)
            Output.ScrollIntoView(Output.ItemCount - 1);
    }
}
