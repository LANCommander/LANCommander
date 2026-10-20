using System.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

/// <summary>
/// Counters, the elevation prompt, and the capture log. Hosted by every step that can run an
/// installer under instrumentation.
/// </summary>
public partial class CapturePanelView : UserControl
{
    public CapturePanelView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Puts the whole capture log on the clipboard.
    /// </summary>
    private async void OnCopyLogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not CaptureStepViewModel viewModel)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard == null)
            return;

        await clipboard.SetTextAsync(viewModel.LogText);
    }

    private async void OnSaveLogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not CaptureStepViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storageProvider == null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save capture log",
            SuggestedFileName = "packaging-capture.log",
            DefaultExtension = "log",
        });

        if (file == null)
            return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);

        await writer.WriteAsync(viewModel.LogText);
    }
}
