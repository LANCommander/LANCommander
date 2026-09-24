using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace LANCommander.Launcher.Helpers;

/// <summary>
/// Converts logical (DIP) image sizes into the device pixels they cover, so art is decoded
/// and requested at the resolution a scaled display actually draws it at.
/// </summary>
public static class DisplayScaling
{
    /// <summary>
    /// Render scaling of <paramref name="visual"/>'s window, else the main window's, else 1.
    /// </summary>
    public static double GetScaling(Visual? visual = null)
    {
        var topLevel = visual != null ? TopLevel.GetTopLevel(visual) : null;

        topLevel ??= Application.Current?.ApplicationLifetime switch
        {
            IClassicDesktopStyleApplicationLifetime desktop => desktop.MainWindow,
            ISingleViewApplicationLifetime singleView when singleView.MainView != null => TopLevel.GetTopLevel(singleView.MainView),
            _ => null,
        };

        return topLevel is { RenderScaling: > 0 } ? topLevel.RenderScaling : 1;
    }

    /// <summary>Device pixels covered by <paramref name="logical"/> DIPs at <paramref name="scaling"/>; zero stays zero.</summary>
    public static int ToPixels(double logical, double scaling) =>
        logical > 0 ? (int)Math.Ceiling(logical * scaling) : 0;

    public static int ToPixels(double logical, Visual? visual = null) =>
        ToPixels(logical, GetScaling(visual));
}
