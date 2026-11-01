using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using LANCommander.Launcher.ViewModels;

namespace LANCommander.Launcher.Views;

public partial class PluginsView : UserControl
{
    public PluginsView()
    {
        InitializeComponent();
    }

    private async void InstallFromFile_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PluginsViewModel viewModel || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Install a Plugin",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Plugin packages")
                {
                    Patterns = ["*.nupkg"],
                },
                FilePickerFileTypes.All,
            ],
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;

        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.InstallFromFileAsync(path);
    }

    private void Search_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || DataContext is not PluginsViewModel viewModel)
            return;

        e.Handled = true;
        viewModel.SearchCommand.Execute(null);
    }
}
