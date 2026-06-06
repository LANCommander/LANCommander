using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LANCommander.Launcher.Views;

public partial class SplashView : UserControl
{
    private static readonly string[] Backgrounds =
    {
        "avares://LANCommander.Launcher/Assets/backgrounds/aoe2.jpg",
        "avares://LANCommander.Launcher/Assets/backgrounds/ns2.jpg",
        "avares://LANCommander.Launcher/Assets/backgrounds/css.jpg",
        "avares://LANCommander.Launcher/Assets/backgrounds/bfme2.jpg",
        "avares://LANCommander.Launcher/Assets/backgrounds/soldat2.jpg",
        "avares://LANCommander.Launcher/Assets/backgrounds/ut2004.jpg",
    };

    public SplashView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            var uri = new Uri(Backgrounds[Random.Shared.Next(Backgrounds.Length)]);
            BackgroundImage.Source = new Bitmap(AssetLoader.Open(uri));
        }
        catch { /* silently ignore missing assets */ }
    }
}
