using System.Collections.ObjectModel;
using Avalonia.Controls;
using LANCommander.Packager.Models;

namespace LANCommander.Packager.Views;

public partial class ActionView : UserControl
{
    private readonly PackageContext _context;
    private readonly ObservableCollection<string> _exeFiles = new();

    public ActionView(PackageContext context)
    {
        _context = context;
        InitializeComponent();
        ExeList.ItemsSource = _exeFiles;
    }

    public void PopulateExecutables()
    {
        _exeFiles.Clear();

        var exes = _context.SelectedFiles
            .Where(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetRelativePath(_context.InstallDirectory, f))
            .Where(f => !IsInstallerExecutable(f))
            .OrderBy(f => f)
            .ToList();

        foreach (var exe in exes)
            _exeFiles.Add(exe);

        if (_exeFiles.Count > 0)
        {
            var bestGuess = FindBestExecutable();
            ExeList.SelectedIndex = bestGuess;
        }
    }

    public void ApplyAction()
    {
        var selected = ExeList.SelectedIndex;
        
        if (selected < 0 || selected >= _exeFiles.Count)
            return;

        var selectedExe = _exeFiles[selected];

        var action = new SDK.Models.Manifest.Action
        {
            Name = ActionNameField.Text ?? "Play",
            Path = selectedExe,
            Arguments = ArgumentsField.Text ?? string.Empty,
            IsPrimaryAction = true,
            SortOrder = 0,
            CreatedOn = DateTime.UtcNow,
            CreatedBy = "LANCommander.Packager"
        };

        _context.Manifest.Actions.Add(action);
    }

    private int FindBestExecutable()
    {
        var noisePatterns = new[] { "redist", "setup", "install", "unins", "directx", "vcredist", "dxsetup", "dotnet" };

        for (int i = 0; i < _exeFiles.Count; i++)
        {
            var lower = _exeFiles[i].ToLowerInvariant();
            
            if (!noisePatterns.Any(p => lower.Contains(p)))
                return i;
        }

        return 0;
    }

    private static bool IsInstallerExecutable(string relativePath)
    {
        var name = Path.GetFileNameWithoutExtension(relativePath).ToLowerInvariant();
        var patterns = new[] { "unins", "setup", "install", "vcredist", "dxsetup", "dotnetfx" };
        
        return patterns.Any(p => name.Contains(p));
    }
}
