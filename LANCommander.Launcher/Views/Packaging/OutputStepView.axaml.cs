using System.IO;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

public partial class OutputStepView : UserControl
{
    public OutputStepView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not OutputStepViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;

        if (storageProvider == null)
            return;

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save package as",
            SuggestedFileName = Path.GetFileName(viewModel.OutputPath),
            DefaultExtension = "lcx",
            FileTypeChoices =
            [
                new FilePickerFileType("LANCommander package")
                {
                    Patterns = ["*.lcx"],
                },
            ],
        });

        var path = file?.TryGetLocalPath();

        if (!string.IsNullOrWhiteSpace(path))
            viewModel.OutputPath = path;
    }
}
