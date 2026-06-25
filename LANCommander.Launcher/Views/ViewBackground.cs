using System;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace LANCommander.Launcher.Views;

/// <summary>
/// Picks a random full-screen background for the Login, Splash and ServerSelection
/// views. Visual-regression tests disable the random pick via <see cref="Enabled"/>
/// so the rendered output (and therefore the committed baseline) stays deterministic.
/// </summary>
internal static class ViewBackground
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

    /// <summary>
    /// When false, no random background is loaded. Set by visual-regression tests so
    /// the rendered output is deterministic across runs.
    /// </summary>
    public static bool Enabled { get; set; } = true;

    public static void Apply(Image target)
    {
        if (!Enabled)
            return;

        try
        {
            var uri = new Uri(Backgrounds[Random.Shared.Next(Backgrounds.Length)]);
            target.Source = new Bitmap(AssetLoader.Open(uri));
        }
        catch { /* silently ignore missing assets */ }
    }
}
