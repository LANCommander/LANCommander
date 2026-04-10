using Avalonia.Controls;
using Avalonia.Interactivity;
using LANCommander.Launcher.Avalonia.ViewModels;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class InstallOptionsWindow : Window
{
    public InstallOptionsWindow()
    {
        InitializeComponent();
    }

    private void Confirm_Click(object? sender, RoutedEventArgs e) => Close(true);

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(false);

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
