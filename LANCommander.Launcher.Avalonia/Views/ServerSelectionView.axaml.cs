using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LANCommander.Launcher.Avalonia.Views;

public partial class ServerSelectionView : UserControl
{
    private static readonly string[] Backgrounds =
    {
        "avares://LANCommander.Launcher.Avalonia/Assets/backgrounds/aoe2.jpg",
        "avares://LANCommander.Launcher.Avalonia/Assets/backgrounds/ns2.jpg",
        "avares://LANCommander.Launcher.Avalonia/Assets/backgrounds/css.jpg",
        "avares://LANCommander.Launcher.Avalonia/Assets/backgrounds/bfme2.jpg",
        "avares://LANCommander.Launcher.Avalonia/Assets/backgrounds/soldat2.jpg",
        "avares://LANCommander.Launcher.Avalonia/Assets/backgrounds/ut2004.jpg",
    };
    
    public ServerSelectionView()
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
        
        ServerAddressTextBox.Focus();
    }
}
