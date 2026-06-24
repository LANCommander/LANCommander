using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LANCommander.Launcher.Views;

public partial class SplashView : UserControl
{
    public SplashView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ViewBackground.Apply(BackgroundImage);
    }
}
