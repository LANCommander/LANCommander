using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
}
