#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace LANCommander.Launcher.Fixtures;

/// <summary>
/// Stand-in artwork for fixture games: covers, backgrounds, icons, logos and screenshots, drawn from
/// the title so the same game always gets the same colors. Views load media from file paths, so each
/// image is rendered to a PNG under the temp directory the first time a process asks for it.
/// </summary>
public static class FixtureArt
{
    private static readonly string Directory = Path.Combine(Path.GetTempPath(), "LANCommander.Fixtures", "Art");

    private static readonly FontFamily Font = new("avares://LANCommander.Launcher/Assets/Fonts/Archivo#Archivo");

    // Rendered this process, so a change to the drawing code is never masked by an old file.
    private static readonly Dictionary<string, string> Rendered = new();

    public static string Cover(string title) =>
        Render($"cover-{Slug(title)}", 600, 900, () => CoverVisual(title));

    public static string Background(string title) =>
        Render($"background-{Slug(title)}", 1600, 900, () => SceneVisual(title, 0));

    public static string Screenshot(string title, int index) =>
        Render($"screenshot-{Slug(title)}-{index}", 1280, 720, () => SceneVisual(title, index + 1));

    public static string Icon(string title) =>
        Render($"icon-{Slug(title)}", 128, 128, () => IconVisual(title));

    public static string Logo(string title) =>
        Render($"logo-{Slug(title)}", 900, 300, () => LogoVisual(title));

    public static string Avatar(string name) =>
        Render($"avatar-{Slug(name)}", 128, 128, () => IconVisual(name));

    /// <summary>Stable across processes, unlike <see cref="string.GetHashCode()"/>.</summary>
    public static uint Hash(string value)
    {
        var hash = 2166136261u;

        foreach (var b in Encoding.UTF8.GetBytes(value))
            hash = (hash ^ b) * 16777619u;

        return hash;
    }

    private static double Hue(string title) => Hash(title) % 360;

    private static Color Hsl(double hue, double saturation, double lightness) =>
        new HslColor(1, (hue % 360 + 360) % 360, saturation, lightness).ToRgb();

    private static string Render(string key, int width, int height, Func<Control> build)
    {
        lock (Rendered)
        {
            if (Rendered.TryGetValue(key, out var existing))
                return existing;

            System.IO.Directory.CreateDirectory(Directory);

            var path = Path.Combine(Directory, key + ".png");
            var size = new Size(width, height);
            var visual = build();

            visual.Measure(size);
            visual.Arrange(new Rect(size));

            using (var bitmap = new RenderTargetBitmap(new PixelSize(width, height)))
            {
                bitmap.Render(visual);
                bitmap.Save(path);
            }

            Rendered[key] = path;

            return path;
        }
    }

    private static Control CoverVisual(string title)
    {
        var hue = Hue(title);

        return new Panel
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0.4, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Hsl(hue, 0.55, 0.42), 0),
                    new GradientStop(Hsl(hue + 35, 0.6, 0.12), 1),
                },
            },
            Children =
            {
                Shape(hue + 180, 520, -140, 120, 0.18),
                Shape(hue + 60, 380, 260, 460, 0.12),
                new TextBlock
                {
                    Text = title.ToUpperInvariant(),
                    FontFamily = Font,
                    FontSize = 64,
                    FontWeight = FontWeight.Black,
                    LineHeight = 66,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(40, 40, 40, 56),
                },
            },
        };
    }

    private static Control SceneVisual(string title, int variant)
    {
        var hue = Hue(title) + variant * 47;

        return new Panel
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Hsl(hue + 200, 0.45, 0.55), 0),
                    new GradientStop(Hsl(hue + 20, 0.5, 0.35), 0.62),
                    new GradientStop(Hsl(hue, 0.55, 0.1), 1),
                },
            },
            Children =
            {
                Shape(hue + 30, 900, -200, 380, 0.35),
                Shape(hue + 90, 700, 900, 420, 0.25),
                Shape(hue + 10, 1200, 300, 620, 0.5),
            },
        };
    }

    private static Control IconVisual(string title)
    {
        var hue = Hue(title);

        return new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Hsl(hue, 0.6, 0.5), 0),
                    new GradientStop(Hsl(hue + 40, 0.6, 0.28), 1),
                },
            },
            Child = new TextBlock
            {
                Text = Initials(title),
                FontFamily = Font,
                FontSize = 52,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }

    private static Control LogoVisual(string title) => new TextBlock
    {
        Text = title.ToUpperInvariant(),
        FontFamily = Font,
        FontSize = 88,
        FontWeight = FontWeight.Black,
        Foreground = Brushes.White,
        TextWrapping = TextWrapping.Wrap,
        TextAlignment = TextAlignment.Center,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>A soft ellipse, for some structure in the gradients.</summary>
    private static Control Shape(double hue, double diameter, double left, double top, double opacity) => new Canvas
    {
        Children =
        {
            new Avalonia.Controls.Shapes.Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = new SolidColorBrush(Hsl(hue, 0.5, 0.5)),
                Opacity = opacity,
                [Canvas.LeftProperty] = left,
                [Canvas.TopProperty] = top,
            },
        },
    };

    private static string Initials(string title)
    {
        var initials = new StringBuilder();

        foreach (var word in title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (char.IsLetterOrDigit(word[0]))
                initials.Append(char.ToUpperInvariant(word[0]));

            if (initials.Length == 2)
                break;
        }

        return initials.ToString();
    }

    private static string Slug(string title)
    {
        var slug = new StringBuilder();

        foreach (var c in title.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                slug.Append(c);
            else if (slug.Length > 0 && slug[^1] != '-')
                slug.Append('-');
        }

        return slug.ToString().Trim('-');
    }
}
#endif
