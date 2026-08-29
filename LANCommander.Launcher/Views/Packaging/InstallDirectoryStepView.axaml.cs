using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

public partial class InstallDirectoryStepView : UserControl
{
    public InstallDirectoryStepView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not InstallDirectoryStepViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storageProvider == null)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose the install folder",
            AllowMultiple = false,
        });

        if (folders.Count == 0)
            return;

        var path = folders[0].TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(path))
            viewModel.InstallDirectory = path;
    }
}
