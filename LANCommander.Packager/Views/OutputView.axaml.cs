using System.IO.Compression;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using LANCommander.Packager.Models;
using LANCommander.Packager.Services;

namespace LANCommander.Packager.Views;

public partial class OutputView : UserControl
{
    private readonly PackageContext _context;

    public OutputView(PackageContext context)
    {
        _context = context;
        InitializeComponent();

        GenerateButton.Click += async (s, e) =>
        {
            await GeneratePackageAsync();
        };

        BrowseButton.Click += async (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save .LCX Package",
                DefaultExtension = "lcx",
                FileTypeChoices = [new("LCX Package") { Patterns = ["*.lcx"] }],
                SuggestedFileName = Path.GetFileName(OutputPathField.Text ?? "Game.lcx")
            });

            if (file != null)
                OutputPathField.Text = file.Path.LocalPath;
        };
    }

    public void SetDefaultOutputPath()
    {
        var title = _context.Manifest.Title ?? "Game";
        var safeName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        OutputPathField.Text = Path.Combine(Environment.CurrentDirectory, $"{safeName}.lcx");
    }

    private void ApplyOptions()
    {
        _context.PatchGameSpy = PatchGameSpyCheck.IsChecked == true;
        _context.WriteSummaryLog = WriteSummaryLogCheck.IsChecked == true;
        _context.CompressionLevel = CompressionLevelCombo.SelectedIndex switch
        {
            0 => CompressionLevel.Optimal,
            1 => CompressionLevel.Fastest,
            2 => CompressionLevel.NoCompression,
            3 => CompressionLevel.SmallestSize,
            _ => CompressionLevel.Optimal
        };
    }

    private async Task GeneratePackageAsync()
    {
        var outputPath = OutputPathField.Text;

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            StatusLabel.Text = "Please specify an output path.";
            return;
        }

        _context.OutputPath = outputPath;
        ApplyOptions();

        GenerateButton.IsEnabled = false;
        Progress.IsVisible = true;
        Progress.Value = 0;

        var progress = new Progress<string>(message =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusLabel.Text = message;

                Progress.Value = message switch
                {
                    "Creating game files archive..." => 0.25,
                    "Generating scripts..." => 0.50,
                    "Writing manifest..." => 0.75,
                    "Done!" => 1.0,
                    _ => Progress.Value
                };
            });
        });

        try
        {
            StatusLabel.Text = "Generating package...";
            await Task.Run(() => LcxBuilderService.BuildAsync(_context, progress));

            var fileInfo = new FileInfo(outputPath);
            var sizeMb = fileInfo.Length / (1024.0 * 1024.0);

            if (_context.WriteSummaryLog)
                WriteSummaryLog(outputPath, sizeMb);

            Dispatcher.UIThread.Post(() =>
            {
                StatusLabel.Text = $"Package created successfully!\n{outputPath}\nSize: {sizeMb:F2} MB";
                GenerateButton.IsEnabled = true;
                Progress.IsVisible = false;
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Post(() =>
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                GenerateButton.IsEnabled = true;
                Progress.IsVisible = false;
            });
        }
    }

    private void WriteSummaryLog(string outputPath, double sizeMb)
    {
        var manifest = _context.Manifest;
        var title = manifest.Title ?? "Game";
        var safeName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        var logDir = Path.GetDirectoryName(outputPath) ?? Environment.CurrentDirectory;
        var logPath = Path.Combine(logDir, $"{safeName}.Package.log");

        var compressionName = _context.CompressionLevel switch
        {
            CompressionLevel.Optimal => "Optimal",
            CompressionLevel.Fastest => "Fastest",
            CompressionLevel.NoCompression => "No Compression",
            CompressionLevel.SmallestSize => "Smallest Size",
            _ => "Optimal"
        };

        var primaryAction = manifest.Actions.FirstOrDefault(a => a.IsPrimaryAction);

        var sb = new StringBuilder();
        sb.AppendLine("LANCommander Packager - Summary Log");
        sb.AppendLine("====================================");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("Source");
        sb.AppendLine($"  Installer:         {_context.InstallerPath}");
        sb.AppendLine($"  Install Directory: {_context.InstallDirectory}");
        sb.AppendLine();
        sb.AppendLine("Metadata");
        sb.AppendLine($"  Title:         {manifest.Title}");

        if (!string.IsNullOrWhiteSpace(manifest.SortTitle) && manifest.SortTitle != manifest.Title)
            sb.AppendLine($"  Sort Title:    {manifest.SortTitle}");

        sb.AppendLine($"  Version:       {manifest.Version}");
        sb.AppendLine($"  Released On:   {manifest.ReleasedOn:yyyy-MM-dd}");
        sb.AppendLine($"  Singleplayer:  {(manifest.Singleplayer ? "Yes" : "No")}");
        sb.AppendLine();
        sb.AppendLine("Contents");
        sb.AppendLine($"  Files Included:    {_context.SelectedFiles.Count}");
        sb.AppendLine($"  Registry Entries:  {_context.SelectedRegistryEntries.Count}");

        if (primaryAction != null)
            sb.AppendLine($"  Primary Action:    {primaryAction.Name} -> {primaryAction.Path}");

        sb.AppendLine();
        sb.AppendLine("Options");
        sb.AppendLine($"  Compression Level: {compressionName}");
        sb.AppendLine($"  Patch GameSpy:     {(_context.PatchGameSpy ? "Yes" : "No")}");
        sb.AppendLine();
        sb.AppendLine("Output");
        sb.AppendLine($"  File: {outputPath}");
        sb.AppendLine($"  Size: {sizeMb:F2} MB");

        try
        {
            File.WriteAllText(logPath, sb.ToString());
        }
        catch
        {
            // Non-critical, ignore write failures
        }
    }
}
