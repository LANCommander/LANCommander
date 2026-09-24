using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ByteSizeLib;
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

    /// <summary>
    /// Files the post-install step found to have been added or changed after the base install.
    /// </summary>
    [ObservableProperty]
    private int _postInstallFileCount;

    public bool HasPostInstallFiles => PostInstallFileCount > 0;

    partial void OnPostInstallFileCountChanged(int value) =>
        OnPropertyChanged(nameof(HasPostInstallFiles));

    /// <summary>Post-install paths the tree was built from, so entering twice is not a rebuild.</summary>
    private int _builtWithPostInstallCount = -1;

    private CheckableTreeNode? _root;

    /// <summary>Install directory the current tree was built from.</summary>
    private string? _builtFor;

    public override void Reset()
    {
        _root = null;
        _builtFor = null;
        _builtWithPostInstallCount = -1;

        Roots.Clear();

        SweptFileCount = 0;
        PostInstallFileCount = 0;
        Summary = string.Empty;
    }

    public override Task OnEnterAsync()
    {
        // Rebuilding on every entry would silently discard the user's selections whenever they
        // stepped back and forward again.
        if (_root == null ||
            !string.Equals(_builtFor, Package.InstallDirectory, StringComparison.OrdinalIgnoreCase) ||
            _builtWithPostInstallCount != Package.PostInstallFiles.Count)
        {
            Rebuild();
        }
        else
        {
            UpdateSummary();
        }

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

        // Only what is still there. A capture records what an installer wrote, but a patch or
        // the user may have deleted some of it since, and an entry for a file that no longer
        // exists is checked by default and then silently contributes nothing to the archive.
        var captured = Package.FileChanges
            .Select(f => f.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var swept = SweepInstallDirectory(installDirectory, captured);

        SweptFileCount = swept.Count;

        var allPaths = captured
            .Concat(swept)
            .Where(p => IsUnder(p, installDirectory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Marked rather than filtered: the user wants to see their no-CD patch sitting in the
        // tree alongside everything the installer put there, not in a list of its own.
        var annotations = BuildPostInstallAnnotations();

        PostInstallFileCount = annotations.Count;

        _root = CheckableTreeNode.BuildFileTree(
            allPaths.Select(p => (p, Path.GetRelativePath(installDirectory, p))),
            annotations,
            GetFileSize);

        // The install folder itself heads the tree, so the whole capture can be toggled from one row.
        _root.Name = installDirectory;
        _root.OnTreeSelectionChanged = UpdateSummary;

        Roots.Clear();

        // A childless root would count itself as one selected file.
        if (_root.Children.Count > 0)
            Roots.Add(_root);

        _builtFor = installDirectory;
        _builtWithPostInstallCount = Package.PostInstallFiles.Count;

        UpdateSummary();
    }

    /// <summary>
    /// Badges for the files the post-install step saw appear or change.
    /// </summary>
    private Dictionary<string, string> BuildPostInstallAnnotations()
    {
        var annotations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (Package.PostInstallFiles.Count == 0)
            return annotations;

        var installed = Package.FileChanges
            .Select(f => f.Path)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var path in Package.PostInstallFiles)
        {
            if (!string.IsNullOrWhiteSpace(path))
                annotations[path] = installed.Contains(path) ? "changed" : "added";
        }

        return annotations;
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

    private static long? GetFileSize(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception)
        {
            return null;
        }
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

        var selectedSize = ByteSize.FromBytes(Roots.Sum(r => r.SumCheckedSize())).ToString("0.##");

        Summary = $"{selected} of {total} file(s) selected · {selectedSize}";

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
