using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewBackground.Apply(BackgroundImage);

        UsernameTextBox.Focus();
    }
}
