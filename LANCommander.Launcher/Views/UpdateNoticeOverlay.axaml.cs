using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Interactivity;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.Services;

namespace LANCommander.Launcher.Views;

/// <summary>
/// Shown once after the launcher updates itself: the release notes for the versions it moved past, or,
/// when the update was undone, why.
/// </summary>
public partial class UpdateNoticeOverlay : UserControl
{
    public event EventHandler? Closed;

    public UpdateNoticeOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, Close);
    }

    /// <param name="releaseNotes">Markdown for the versions installed, or empty when there are none.</param>
    public UpdateNoticeOverlay(UpdateNotice notice, string releaseNotes) : this()
    {
        if (notice.Succeeded)
        {
            TitleBar.Title = $"What's New in LANCommander {notice.Version}";
            SummaryText.Text = notice.PreviousVersion == null
                ? $"The launcher was updated to version {notice.Version}."
                : $"The launcher was updated from version {notice.PreviousVersion} to {notice.Version}.";
        }
        else
        {
            TitleBar.Title = "Update Failed";
            ErrorPanel.IsVisible = true;
            ErrorText.Text = notice.Error;
            SummaryText.Text = notice.PreviousVersion == null
                ? $"The update to version {notice.Version} was undone."
                : $"The update to version {notice.Version} was undone, and version {notice.PreviousVersion} is still installed.";
        }

        if (!string.IsNullOrWhiteSpace(releaseNotes))
        {
            Notes.Text = releaseNotes;
            Notes.IsVisible = true;
        }
    }

    private void OK_Click(object? sender, RoutedEventArgs e) => Close();
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close();

    private void Close()
    {
        var layer = OverlayLayer.GetOverlayLayer(this);
        Closed?.Invoke(this, EventArgs.Empty);
        layer?.Children.Remove(this);
    }

    /// <summary>Shows the notice centered in the main window and waits for the user to dismiss it.</summary>
    public static async Task ShowAsync(UpdateNotice notice, string releaseNotes)
    {
        var tcs = new TaskCompletionSource<bool>();

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new UpdateNoticeOverlay(notice, releaseNotes)
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.Closed += (_, _) => tcs.TrySetResult(true);

            var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var layer = OverlayLayer.GetOverlayLayer(mainWindow!);

            if (layer is not null)
            {
                overlay.Bind(Avalonia.Layout.Layoutable.WidthProperty, new Binding("Bounds.Width") { Source = layer });
                overlay.Bind(Avalonia.Layout.Layoutable.HeightProperty, new Binding("Bounds.Height") { Source = layer });
                layer.Children.Add(overlay);
            }
            else
            {
                tcs.TrySetResult(false);
            }
        });

        await tcs.Task;
    }
}
