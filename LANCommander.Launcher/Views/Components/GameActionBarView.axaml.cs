using System;
using Avalonia.Controls;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.Views.Components;

public partial class GameActionBarView : UserControl
{
    private GameActionBarViewModel? _vm;
    private MenuFlyout? _installFlyout;
    private MenuFlyout? _playFlyout;

    public GameActionBarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Tear down any flyouts built for the previous view model.
        if (_installFlyout != null)
        {
            GameContextMenu.DetachBinder(_installFlyout);
            InstallSplitButton.Flyout = null;
            _installFlyout = null;
        }

        if (_playFlyout != null)
        {
            GameContextMenu.DetachBinder(_playFlyout);
            PlaySplitButton.Flyout = null;
            _playFlyout = null;
        }

        _vm = DataContext as GameActionBarViewModel;

        if (_vm == null)
            return;

        // Both split buttons share the same consolidated, state-driven menu; only one is ever
        // visible at a time, but each needs its own flyout instance.
        _installFlyout = GameContextMenu.CreateFlyout(_vm);
        _playFlyout = GameContextMenu.CreateFlyout(_vm);

        InstallSplitButton.Flyout = _installFlyout;
        PlaySplitButton.Flyout = _playFlyout;
    }
}
