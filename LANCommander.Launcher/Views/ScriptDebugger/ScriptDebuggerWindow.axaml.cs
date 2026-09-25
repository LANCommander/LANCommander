using System;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using LANCommander.Launcher.ViewModels.ScriptDebugger;

namespace LANCommander.Launcher.Views.ScriptDebugger;

public partial class ScriptDebuggerWindow : Window
{
    public static readonly IValueConverter IsPositive =
        new FuncValueConverter<int, bool>(value => value > 0);

    public static readonly IValueConverter RunLabel =
        new FuncValueConverter<bool, string>(stopped => stopped ? "Continue" : "Run");

    private ScriptDebuggerWindowViewModel? _model;

    public ScriptDebuggerWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_model is not null)
            _model.ActivateRequested -= OnActivateRequested;

        _model = DataContext as ScriptDebuggerWindowViewModel;

        if (_model is not null)
            _model.ActivateRequested += OnActivateRequested;
    }

    /// <summary>A script stopped at a breakpoint, most likely while the user was looking at the launcher.</summary>
    private void OnActivateRequested()
    {
        if (WindowState == WindowState.Minimized)
            WindowState = WindowState.Normal;

        Activate();
    }
}
