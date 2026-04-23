using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class InstallOptionsOverlay : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    public InstallOptionsOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
        {
            foreach (var addon in vm.Addons)
                addon.PropertyChanged += OnAddonSelectionChanged;
        }
    }

    private void OnAddonSelectionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallAddonItemViewModel.IsSelected) && DataContext is InstallOptionsViewModel vm)
            vm.RefreshSizes();
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);
    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

    private void Close(bool? result)
    {
        if (DataContext is InstallOptionsViewModel vm)
        {
            foreach (var addon in vm.Addons)
                addon.PropertyChanged -= OnAddonSelectionChanged;
        }

        var layer = OverlayLayer.GetOverlayLayer(this);
        DialogClosed?.Invoke(this, result);
        layer?.Children.Remove(this);
    }

    private void SelectAllAddons_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
            foreach (var addon in vm.Addons)
                addon.IsSelected = true;
    }

    private void DeselectAllAddons_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is InstallOptionsViewModel vm)
            foreach (var addon in vm.Addons)
                addon.IsSelected = false;
    }
}
