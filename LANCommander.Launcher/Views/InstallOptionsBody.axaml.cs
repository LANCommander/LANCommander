using System;
using System.ComponentModel;
using Avalonia.Controls;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views;

/// <summary>
/// Reusable body for the install/modify options: size summary, optional add-ons and tools, and the
/// install-directory picker. Shared by <see cref="InstallOptionsOverlay"/> (fresh install) and the
/// Manage dialog's Modify section. Owns the "All"/"None" buttons and keeps the size summary in sync
/// with the current add-on/tool selection.
/// </summary>
public partial class InstallOptionsBody : UserControl
{
    private InstallOptionsViewModel? _wired;

    public InstallOptionsBody()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Unwire();

        if (DataContext is InstallOptionsViewModel vm)
        {
            _wired = vm;

            foreach (var addon in vm.Addons)
                addon.PropertyChanged += OnSelectionChanged;
            foreach (var tool in vm.Tools)
                tool.PropertyChanged += OnSelectionChanged;
        }
    }

    private void Unwire()
    {
        if (_wired is null)
            return;

        foreach (var addon in _wired.Addons)
            addon.PropertyChanged -= OnSelectionChanged;
        foreach (var tool in _wired.Tools)
            tool.PropertyChanged -= OnSelectionChanged;

        _wired = null;
    }

    private void OnSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if ((e.PropertyName == nameof(InstallAddonItemViewModel.IsSelected)
             || e.PropertyName == nameof(InstallToolItemViewModel.IsSelected))
            && DataContext is InstallOptionsViewModel vm)
            vm.RefreshSizes();
    }

    private void SelectAllAddons_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
            foreach (var addon in vm.Addons)
                addon.IsSelected = true;
    }

    private void DeselectAllAddons_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
            foreach (var addon in vm.Addons)
                addon.IsSelected = false;
    }

    private void SelectAllTools_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
            foreach (var tool in vm.Tools)
                tool.IsSelected = true;
    }

    private void DeselectAllTools_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
            foreach (var tool in vm.Tools)
                tool.IsSelected = false;
    }
}
