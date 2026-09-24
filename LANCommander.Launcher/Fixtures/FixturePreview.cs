#if DEBUG
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using LANCommander.Launcher.Views;

namespace LANCommander.Launcher.Fixtures;

/// <summary>
/// Opens fixtures in the running app instead of the launcher itself, for styling and reviewing
/// screens without a server. Start a Debug build with <c>LANCOMMANDER_FIXTURE</c> set to a
/// fixture's name to open it, or to anything else (<c>all</c>, say) to pick from the list.
/// </summary>
public static class FixturePreview
{
    public const string EnvironmentVariable = "LANCOMMANDER_FIXTURE";

    public static string? Requested =>
        Environment.GetEnvironmentVariable(EnvironmentVariable) is { Length: > 0 } value ? value.Trim() : null;

    public static bool IsRequested => Requested != null;

    public static void Start(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var gallery = new GalleryWindow();

        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.MainWindow = gallery;

        gallery.Show();

        if (Requested is { } name && FixtureCatalog.Find(name) is { } fixture)
            gallery.Select(fixture);
    }

    /// <summary>Lists every fixture; selecting one opens it, replacing whichever was open.</summary>
    private sealed class GalleryWindow : Window
    {
        private readonly ListBox _list;

        private FixtureContext? _context;
        private Window? _window;

        public GalleryWindow()
        {
            Title = "LANCommander Fixtures";
            Width = 420;
            Height = 760;

            _list = new ListBox
            {
                ItemsSource = FixtureCatalog.All,
                ItemTemplate = new FuncDataTemplate<ViewFixture>((fixture, _) => new StackPanel
                {
                    Margin = new Thickness(4, 2),
                    Children =
                    {
                        new TextBlock { Text = fixture?.Name, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Text = fixture?.Description, Opacity = 0.6, TextWrapping = TextWrapping.Wrap },
                    },
                }),
            };

            _list.SelectionChanged += (_, _) =>
            {
                if (_list.SelectedItem is ViewFixture fixture)
                    Open(fixture);
            };

            Content = _list;

            Closed += (_, _) => CloseFixture();
        }

        public void Select(ViewFixture fixture) => _list.SelectedItem = fixture;

        private void Open(ViewFixture fixture)
        {
            CloseFixture();

            // Each fixture gets its own services, as it does under test.
            _context = FixtureContext.Create();
            _window = FixtureHost.Show(fixture, _context);
        }

        private void CloseFixture()
        {
            // The main window hides to the tray when closed normally.
            if (_window is MainWindow main)
                main.ExitApplication();
            else
                _window?.Close();

            _context?.Dispose();

            _window = null;
            _context = null;
        }
    }
}
#endif
