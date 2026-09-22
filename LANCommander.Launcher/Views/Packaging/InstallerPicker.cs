using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LANCommander.Launcher.ViewModels.Packaging;

namespace LANCommander.Launcher.Views.Packaging;

/// <summary>
/// Picks an executable and hands it to a capture step to run under instrumentation.
/// </summary>
internal static class InstallerPicker
{
    public static async Task PickAndStartAsync(Visual owner, CaptureStepViewModel viewModel, string title)
    {
        var storageProvider = TopLevel.GetTopLevel(owner)?.StorageProvider;

        if (storageProvider == null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
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
