using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class ServerSelectionView : UserControl
{
    public ServerSelectionView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ServerAddressTextBox.Focus();
    }
}
