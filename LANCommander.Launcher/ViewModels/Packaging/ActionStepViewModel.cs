using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Picks the executable the launcher will run to start the game.
/// </summary>
public partial class ActionStepViewModel : PackagingStepViewModel
{
    /// <summary>
    /// Executables that are almost never the game itself. Filtering them out means the right
    /// entry is usually preselected instead of buried among redistributables.
    /// </summary>
    private static readonly string[] NonGameExecutableHints =
    [
        "unins",
        "setup",
        "install",
        "vcredist",
        "dxsetup",
        "dotnetfx",
        "directx",
        "crashreport",
        "crashhandler",
    ];

    public ActionStepViewModel(PackagingWizardViewModel wizard) : base(wizard)
    {
    }

    public override string Title => "Launch Action";

    public ObservableCollection<string> Executables { get; } = [];

    [ObservableProperty]
    private string? _selectedExecutable;

    [ObservableProperty]
    private string _actionName = "Play";

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private bool _showAllExecutables;

    [ObservableProperty]
    private string _summary = string.Empty;

    private List<string> _allExecutables = [];

    partial void OnSelectedExecutableChanged(string? value) =>
        CanGoNext = !string.IsNullOrWhiteSpace(value);

    partial void OnShowAllExecutablesChanged(bool value) => PopulateExecutables();

    public override Task OnEnterAsync()
    {
        _allExecutables = Package.SelectedFiles
            .Where(p => p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PopulateExecutables();

        return Task.CompletedTask;
    }

    private void PopulateExecutables()
    {
        var previous = SelectedExecutable;

        var candidates = ShowAllExecutables
            ? _allExecutables
            : [.. _allExecutables.Where(IsLikelyGameExecutable)];

        // Never hide everything: if the filter leaves nothing, show the unfiltered list.
        if (candidates.Count == 0)
            candidates = _allExecutables;

        Executables.Clear();

        foreach (var executable in candidates)
            Executables.Add(executable);

        SelectedExecutable = previous != null && Executables.Contains(previous)
            ? previous
            : Executables.FirstOrDefault();

        Summary = _allExecutables.Count == 0
            ? "No executables were found among the selected files."
            : $"{Executables.Count} of {_allExecutables.Count} executable(s) shown.";

        CanGoNext = !string.IsNullOrWhiteSpace(SelectedExecutable);
    }

    private static bool IsLikelyGameExecutable(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);

        return !NonGameExecutableHints.Any(hint =>
            name.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    public override Task OnLeaveAsync()
    {
        Package.Manifest.Actions = [];

        if (string.IsNullOrWhiteSpace(SelectedExecutable))
            return Task.CompletedTask;

        // Paths in the manifest are relative to the install directory, which is where the
        // launcher expands the archive on a player's machine.
        var relativePath = Path.GetRelativePath(Package.InstallDirectory, SelectedExecutable);

        Package.Manifest.Actions.Add(new SDK.Models.Manifest.Action
        {
            Name = string.IsNullOrWhiteSpace(ActionName) ? "Play" : ActionName,
            Path = relativePath,
            Arguments = Arguments,
            WorkingDirectory = string.Empty,
            IsPrimaryAction = true,
            SortOrder = 0,
        });

        return Task.CompletedTask;
    }
}
