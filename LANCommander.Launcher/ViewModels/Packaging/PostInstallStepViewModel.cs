using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services.Packaging;
using LANCommander.Packaging.Analysis;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// The stage between "the installer finished" and "this is what I want to ship": no-CD patches,
/// widescreen fixes, edited configs, mods, and anything that arrives as its own installer.
/// </summary>
public partial class PostInstallStepViewModel : CaptureStepViewModel
{
    /// <summary>Beyond this the list stops being something a person reads.</summary>
    private const int MaxListedChanges = 2000;

    private DirectorySnapshot? _baseline;

    /// <summary>Install folder the baseline was taken of.</summary>
    private string? _baselineFor;

    /// <summary>
    /// Every added or modified path from the last scan, uncapped.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Changes"/>, which is trimmed for display. A mod that drops in
    /// five thousand files still has all five thousand marked in the file step; only the list
    /// the user reads is shortened.
    /// </remarks>
    private List<string> _postInstallPaths = [];

    public PostInstallStepViewModel(PackagingWizardViewModel wizard, IServiceProvider serviceProvider)
        : base(wizard, serviceProvider)
    {
    }

    public override string Title => "Customize";

    /// <summary>
    /// Needs somewhere to watch. Without a detected install folder there is nothing to compare
    /// against, and the step would be a dead end rather than an optional one.
    /// </summary>
    public override bool IsApplicable => !string.IsNullOrWhiteSpace(Package.InstallDirectory);

    /// <summary>Plenty of games need no patching at all, so the step is skippable.</summary>
    protected override bool CaptureIsOptional => true;

    protected override string MonitoringStatus =>
        "Monitoring. Complete the patch or mod install, then choose Stop.";

    protected override string CaptureFinishedStatus =>
        "Patch capture finished. Run another, make more changes by hand, or continue.";

    public string InstallDirectory => Package.InstallDirectory;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _scanSummary = string.Empty;

    /// <summary>Files in the folder when the step was entered.</summary>
    [ObservableProperty]
    private int _baselineFileCount;

    [ObservableProperty]
    private int _addedCount;

    [ObservableProperty]
    private int _modifiedCount;

    [ObservableProperty]
    private int _removedCount;

    /// <summary>True once a rescan has run, so "no changes" can be told from "not looked yet".</summary>
    [ObservableProperty]
    private bool _hasScanned;

    /// <summary>The diff, flattened for display. Capped; the counters above stay exact.</summary>
    public ObservableCollection<DirectoryChangeItem> Changes { get; } = [];

    /// <summary>Installers run from this step, newest last.</summary>
    public ObservableCollection<string> AdditionalInstallers { get; } = [];

    public bool HasChanges => AddedCount + ModifiedCount + RemovedCount > 0;

    public bool HasAdditionalInstallers => AdditionalInstallers.Count > 0;

    /// <summary>Set when the diff was too long to list in full.</summary>
    [ObservableProperty]
    private bool _isChangeListTruncated;

    partial void OnAddedCountChanged(int value) => OnPropertyChanged(nameof(HasChanges));

    partial void OnModifiedCountChanged(int value) => OnPropertyChanged(nameof(HasChanges));

    partial void OnRemovedCountChanged(int value) => OnPropertyChanged(nameof(HasChanges));

    public override void Reset()
    {
        base.Reset();

        _baseline = null;
        _baselineFor = null;

        BaselineFileCount = 0;
        ScanSummary = string.Empty;

        // Left empty: the status line is only shown once a capture has something to say, so a
        // step nobody has run an installer through stays quiet.
        Status = string.Empty;

        AdditionalInstallers.Clear();

        OnPropertyChanged(nameof(HasAdditionalInstallers));

        ClearDiff();
    }

    public override async Task OnEnterAsync()
    {
        OnPropertyChanged(nameof(InstallDirectory));

        if (_baseline != null &&
            string.Equals(_baselineFor, Package.InstallDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // Returning to the step after going forward and back. Rebaselining here would erase
            // the record of everything the user had already done.
            return;
        }

        await CaptureBaselineAsync();
    }

    private async Task CaptureBaselineAsync()
    {
        var directory = Package.InstallDirectory;

        if (string.IsNullOrWhiteSpace(directory))
            return;

        IsScanning = true;
        ScanSummary = "Taking a snapshot of the install folder...";

        try
        {
            _baseline = await DirectoryScanner.CaptureAsync(directory);
            _baselineFor = directory;

            BaselineFileCount = _baseline.FileCount;

            ClearDiff();

            ScanSummary = _baseline.RootExists
                ? $"Baseline taken: {BaselineFileCount:N0} file(s) in {directory}."
                : $"{directory} does not exist yet.";
        }
        catch (Exception ex)
        {
            ScanSummary = $"Could not read the install folder: {ex.Message}";

            Logger.LogError(ex, "Could not baseline the install directory {Directory}", directory);
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>
    /// Compares the install folder against the baseline. Cheap enough to press repeatedly: a
    /// snapshot is one directory walk and reads no file contents.
    /// </summary>
    [RelayCommand]
    private async Task RescanAsync()
    {
        if (IsScanning || _baseline == null)
            return;

        IsScanning = true;
        ScanSummary = "Scanning for changes...";

        try
        {
            var current = await DirectoryScanner.CaptureAsync(Package.InstallDirectory);
            var diff = DirectoryScanner.Diff(_baseline, current);

            ApplyDiff(diff);

            HasScanned = true;
        }
        catch (Exception ex)
        {
            ScanSummary = $"Could not scan the install folder: {ex.Message}";

            Logger.LogError(ex, "Could not scan the install directory for changes");
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void ApplyDiff(DirectoryDiff diff)
    {
        AddedCount = diff.Added.Count;
        ModifiedCount = diff.Modified.Count;
        RemovedCount = diff.Removed.Count;

        _postInstallPaths = [.. diff.AddedOrModified.Select(c => c.Path)];

        Changes.Clear();

        foreach (var change in diff.All.Take(MaxListedChanges))
            Changes.Add(new DirectoryChangeItem(change));

        IsChangeListTruncated = diff.TotalCount > MaxListedChanges;

        ScanSummary = diff.IsEmpty
            ? $"No changes since the install finished. ({BaselineFileCount:N0} file(s) in the folder.)"
            : $"{diff.Added.Count:N0} added, {diff.Modified.Count:N0} changed, {diff.Removed.Count:N0} removed.";

        OnPropertyChanged(nameof(HasChanges));
    }

    private void ClearDiff()
    {
        _postInstallPaths = [];

        AddedCount = 0;
        ModifiedCount = 0;
        RemovedCount = 0;

        Changes.Clear();

        IsChangeListTruncated = false;
        HasScanned = false;
    }

    /// <summary>
    /// Re-baselines against the folder as it stands now, dropping the recorded changes.
    /// </summary>
    [RelayCommand]
    private async Task ResetBaselineAsync()
    {
        _baseline = null;
        _baselineFor = null;

        await CaptureBaselineAsync();
    }

    /// <summary>
    /// Opens the install folder so the user can drop files in or edit configs.
    /// </summary>
    [RelayCommand]
    private void BrowseInstallFolder()
    {
        var directory = Package.InstallDirectory;

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            ScanSummary = "The install folder could not be found.";

            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            ScanSummary = $"Could not open the install folder: {ex.Message}";

            Logger.LogError(ex, "Could not open the install directory {Directory}", directory);
        }
    }

    /// <summary>
    /// Records the installer that was just monitored and folds its work into the same review as
    /// the hand-made changes.
    /// </summary>
    protected override async Task OnCaptureStoppedAsync(PackagingSessionSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(InstallerPath) &&
            !AdditionalInstallers.Contains(InstallerPath, StringComparer.OrdinalIgnoreCase))
        {
            AdditionalInstallers.Add(InstallerPath);

            OnPropertyChanged(nameof(HasAdditionalInstallers));
        }

        await RescanAsync();
    }

    /// <summary>
    /// Hands the file step the paths that changed after the base install, and the package the
    /// record of what was run.
    /// </summary>
    public override async Task OnLeaveAsync()
    {
        // Read before the base stops any capture in progress: stopping rescans on its own, and
        // walking a twenty-gigabyte install twice on one click of Next is worth avoiding.
        var wasMonitoring = IsMonitoring;

        await base.OnLeaveAsync();

        if (_baseline != null && !wasMonitoring && !IsScanning)
            await RescanAsync();

        Package.PostInstallFiles = [.. _postInstallPaths];
        Package.AdditionalInstallers = [.. AdditionalInstallers];
    }
}

/// <summary>
/// One row of the change list.
/// </summary>
public class DirectoryChangeItem
{
    public DirectoryChangeItem(DirectoryChange change)
    {
        Kind = change.Kind;
        Path = change.Path;
        RelativePath = change.RelativePath;
        Size = FormatSize(change);
    }

    public DirectoryChangeKind Kind { get; }

    public string Path { get; }

    public string RelativePath { get; }

    /// <summary>Preformatted: a converter per row would run on every scroll.</summary>
    public string Size { get; }

    public bool IsAdded => Kind == DirectoryChangeKind.Added;

    public bool IsModified => Kind == DirectoryChangeKind.Modified;

    public bool IsRemoved => Kind == DirectoryChangeKind.Removed;

    public string Label => Kind switch
    {
        DirectoryChangeKind.Added => "added",
        DirectoryChangeKind.Modified => "changed",
        _ => "removed",
    };

    /// <summary>
    /// Shows the delta for a modification, because "the exe grew by 4 KB" is what tells the user
    /// the patch landed.
    /// </summary>
    private static string FormatSize(DirectoryChange change)
    {
        if (change.Kind != DirectoryChangeKind.Modified)
            return Humanize(change.Length);

        var delta = change.LengthDelta;

        return delta == 0
            ? Humanize(change.Length)
            : $"{Humanize(change.Length)}  ({(delta > 0 ? "+" : "-")}{Humanize(Math.Abs(delta))})";
    }

    private static string Humanize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];

        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
    }
}
