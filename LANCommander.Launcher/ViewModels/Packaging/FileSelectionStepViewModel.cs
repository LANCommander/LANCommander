using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.ViewModels.Components;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Chooses which files go into the package.
/// </summary>
public partial class FileSelectionStepViewModel : PackagingStepViewModel
{
    public FileSelectionStepViewModel(PackagingWizardViewModel wizard) : base(wizard)
    {
    }

    public override string Title => "Files";

    public ObservableCollection<CheckableTreeNode> Roots { get; } = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>
    /// Files found on disk that the capture never saw, offered pre-checked.
    /// </summary>
    [ObservableProperty]
    private int _sweptFileCount;

    public bool HasSweptFiles => SweptFileCount > 0;

    partial void OnSweptFileCountChanged(int value) => OnPropertyChanged(nameof(HasSweptFiles));

    private CheckableTreeNode? _root;

    /// <summary>Install directory the current tree was built from.</summary>
    private string? _builtFor;

    public override Task OnEnterAsync()
    {
        // Rebuilding on every entry would silently discard the user's selections whenever they
        // stepped back and forward again.
        if (_root == null || !string.Equals(_builtFor, Package.InstallDirectory, StringComparison.OrdinalIgnoreCase))
            Rebuild();
        else
            UpdateSummary();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Builds the tree from the capture, plus anything found in the install directory that the
    /// capture missed.
    /// </summary>
    /// <remarks>
    /// The disk sweep matters. Instrumenting child processes is a poll-and-inject race, so a
    /// short-lived installer stage can write files that were never observed. Sweeping the
    /// detected install directory afterwards costs nothing and recovers essentially all of them.
    /// </remarks>
    private void Rebuild()
    {
        var installDirectory = Package.InstallDirectory;

        var captured = Package.FileChanges
            .Select(f => f.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var swept = SweepInstallDirectory(installDirectory, captured);

        SweptFileCount = swept.Count;

        var allPaths = captured
            .Concat(swept)
            .Where(p => IsUnder(p, installDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _root = CheckableTreeNode.BuildFileTree(
            allPaths.Select(p => (p, Path.GetRelativePath(installDirectory, p))));

        _root.OnTreeSelectionChanged = UpdateSummary;

        Roots.Clear();

        foreach (var child in _root.Children)
            Roots.Add(child);

        _builtFor = installDirectory;

        UpdateSummary();
    }

    private static List<string> SweepInstallDirectory(string installDirectory, HashSet<string> captured)
    {
        var found = new List<string>();

        if (string.IsNullOrWhiteSpace(installDirectory) || !Directory.Exists(installDirectory))
            return found;

        try
        {
            foreach (var path in Directory.EnumerateFiles(installDirectory, "*", SearchOption.AllDirectories))
            {
                if (!captured.Contains(path))
                    found.Add(path);
            }
        }
        catch (Exception)
        {
            // An unreadable subdirectory should narrow the sweep, not fail the step.
        }

        return found;
    }

    private static bool IsUnder(string path, string directory) =>
        !string.IsNullOrWhiteSpace(directory) &&
        path.StartsWith(directory, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void SelectAll() => SetAll(true);

    [RelayCommand]
    private void SelectNone() => SetAll(false);

    [RelayCommand]
    private void Refresh() => Rebuild();

    private void SetAll(bool value)
    {
        foreach (var node in Roots)
            node.IsChecked = value;

        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var selected = Roots.Sum(r => r.CountCheckedLeaves());
        var total = Roots.Sum(r => r.CountTotalLeaves());

        Summary = $"{selected} of {total} file(s) selected.";

        CanGoNext = selected > 0;
    }

    public override Task OnLeaveAsync()
    {
        Package.SelectedFiles = Roots
            .SelectMany(r => r.GetCheckedLeaves())
            .Select(n => n.FullPath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.CompletedTask;
    }
}
