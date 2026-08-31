using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

public partial class MonitorStepView : UserControl
{
    public MonitorStepView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// File picking lives in the view: it needs the storage provider off the top-level window,
    /// which a view model has no business reaching for.
    /// </summary>
    private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MonitorStepViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an installer to monitor",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Installers")
                {
                    Patterns = ["*.exe", "*.msi"],
                },
                FilePickerFileTypes.All,
            ],
        });

        if (files.Count == 0)
            return;

        var path = files[0].TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.SetInstallerAsync(path);
    }

    /// <summary>
    /// Puts the whole capture log on the clipboard.
    /// </summary>
    /// <remarks>
    /// Clipboard access hangs off the top level window, so this belongs in the view rather than
    /// the view model.
    /// </remarks>
    private async void OnCopyLogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MonitorStepViewModel viewModel)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

        if (clipboard == null)
            return;

        await clipboard.SetTextAsync(viewModel.LogText);
    }

    private async void OnSaveLogClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not MonitorStepViewModel viewModel)
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
