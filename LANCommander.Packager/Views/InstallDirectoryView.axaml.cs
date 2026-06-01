using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LANCommander.Packager.Models;
using LANCommander.Packager.Services;

namespace LANCommander.Packager.Views;

public partial class InstallDirectoryView : UserControl
{
    private readonly PackageContext _context;

    public InstallDirectoryView(PackageContext context)
    {
        _context = context;
        InitializeComponent();

        BrowseButton.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            
            if (topLevel == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Install Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0)
                DirectoryField.Text = folders[0].Path.LocalPath;
        };
    }

    public void PopulateFromMonitor(InstallerMonitorService? monitor)
    {
        if (monitor != null)
        {
            var detected = monitor.DetectInstallDirectory();

            DirectoryField.Text = detected;
            _context.InstallDirectory = detected;
        }
    }

    public void PopulateFromDetectedPath(string detectedPath)
    {
        DirectoryField.Text = detectedPath;
        _context.InstallDirectory = detectedPath;
    }

    public void ApplySelection()
    {
        _context.InstallDirectory = DirectoryField.Text ?? string.Empty;
    }
}
