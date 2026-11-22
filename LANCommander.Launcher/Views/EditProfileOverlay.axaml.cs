using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LANCommander.Launcher.Controls;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views;

/// <summary>
/// Changes the player's alias and avatar. A picked avatar is uploaded straight away; the alias is
/// sent with Save, which closes the dialog once the server has taken it.
/// </summary>
public partial class EditProfileOverlay : UserControl
{
    /// <summary>Raised when the dialog closes.</summary>
    public event EventHandler? DialogClosed;

    public EditProfileOverlay()
    {
        InitializeComponent();
        ModalEscape.Enable(this, Close);
    }

    public EditProfileOverlay(EditProfileViewModel viewModel) : this()
    {
        DataContext = viewModel;

        viewModel.CloseRequested += OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();

    private async void ChangeAvatar_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not EditProfileViewModel viewModel || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
            return;

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose an Avatar",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("Images")
                    {
                        Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp"],
                        MimeTypes = ["image/png", "image/jpeg", "image/webp"],
                    },
                ],
            });

            var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;

            if (!string.IsNullOrWhiteSpace(path))
                await viewModel.ChangeAvatarAsync(path);
        }
        catch
        {
            // No picker on this platform, or the shell dialog failed; the avatar stays as it was
        }
    }

    private void Alias_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not EditProfileViewModel viewModel)
            return;

        e.Handled = true;

        if (viewModel.SaveAliasCommand.CanExecute(null))
            viewModel.SaveAliasCommand.Execute(null);
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close();
    private void TitleBar_CloseRequested(object? sender, EventArgs e) => Close();

    private void Close()
    {
        // Closing mid-request would hide the outcome, so wait for it
        if (DataContext is EditProfileViewModel { IsBusy: true })
            return;

        if (DataContext is EditProfileViewModel viewModel)
            viewModel.CloseRequested -= OnCloseRequested;

        var layer = OverlayLayer.GetOverlayLayer(this);
        DialogClosed?.Invoke(this, EventArgs.Empty);
        layer?.Children.Remove(this);
    }

    /// <summary>Shows the dialog over the main window, and returns once it closes.</summary>
    public static async Task ShowAsync(EditProfileViewModel viewModel)
    {
        var tcs = new TaskCompletionSource();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var overlay = new EditProfileOverlay(viewModel)
            {
                HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            };

            overlay.DialogClosed += (_, _) => tcs.TrySetResult();

            var mainWindow = (Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var layer = OverlayLayer.GetOverlayLayer(mainWindow);

            if (layer is not null)
            {
                overlay.Bind(WidthProperty, new Binding("Bounds.Width") { Source = layer });
                overlay.Bind(HeightProperty, new Binding("Bounds.Height") { Source = layer });
                layer.Children.Add(overlay);
            }
            else
            {
                tcs.TrySetResult();
            }
        });

        await tcs.Task;
    }
}
