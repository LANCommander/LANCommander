using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Defines the entry points the launcher offers for the game.
/// </summary>
/// <remarks>
/// A game routinely needs more than one: singleplayer and multiplayer executables, a dedicated
/// server, a configuration tool. This mirrors the server's action editor so a package built
/// here and a game edited there describe their actions the same way.
/// </remarks>
public partial class ActionStepViewModel : PackagingStepViewModel
{
    /// <summary>
    /// Expanded by the launcher to wherever it put the game. The documented default for an
    /// action's working directory, and what the server's editor fills in.
    /// </summary>
    public const string InstallDirectoryVariable = "{InstallDir}";

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

    private static readonly string[] ExecutableExtensions = [".exe", ".bat", ".cmd", ".com"];

    /// <summary>Install-directory-relative paths of every file going into the package.</summary>
    private HashSet<string> _packagedFiles = new(StringComparer.OrdinalIgnoreCase);

    private List<string> _allExecutables = [];

    /// <summary>
    /// Guards the primary flag against its own cascade: clearing the previous primary raises a
    /// change notification that would otherwise re-enter and pick a new one mid-update.
    /// </summary>
    private bool _syncingPrimary;

    public ActionStepViewModel(PackagingWizardViewModel wizard) : base(wizard)
    {
    }

    public override string Title => "Actions";

    /// <summary>The rows of the editor, in launch order.</summary>
    public ObservableCollection<ActionEntryViewModel> Actions { get; } = [];

    /// <summary>
    /// Candidate paths offered in every row's dropdown. Shared rather than per-row, because the
    /// filter above the list applies to the whole editor as it does on the server.
    /// </summary>
    public ObservableCollection<string> Executables { get; } = [];

    [ObservableProperty]
    private bool _showAllExecutables;

    [ObservableProperty]
    private string _summary = string.Empty;

    /// <summary>
    /// Outcome of the last test launch. Kept apart from <see cref="Summary"/> so the next edit
    /// to a row does not wipe out the reason a test failed.
    /// </summary>
    [ObservableProperty]
    private string _testStatus = string.Empty;

    public bool HasTestStatus => !string.IsNullOrEmpty(TestStatus);

    partial void OnTestStatusChanged(string value) => OnPropertyChanged(nameof(HasTestStatus));

    public bool HasActions => Actions.Count > 0;

    partial void OnShowAllExecutablesChanged(bool value) => PopulateExecutables();

    public override void Reset()
    {
        _allExecutables = [];
        _packagedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Actions.Clear();
        Executables.Clear();

        ShowAllExecutables = false;
        Summary = string.Empty;
        TestStatus = string.Empty;

        OnPropertyChanged(nameof(HasActions));
    }

    public override Task OnEnterAsync()
    {
        _packagedFiles = Package.SelectedFiles
            .Select(ToRelativePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _allExecutables = _packagedFiles
            .Where(p => ExecutableExtensions.Any(e => p.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        PopulateExecutables();

        // Seeded only when empty, so stepping back and forward does not throw away rows the
        // user has already filled in.
        if (Actions.Count == 0)
            SeedFirstAction();

        // The file selection may have changed since these rows were filled in, which is exactly
        // when an action ends up aimed at a file that is no longer in the package.
        foreach (var action in Actions)
            action.RefreshDerivedState();

        UpdateSummary();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts the user off with the most likely entry point, so a single-executable game needs
    /// no interaction here at all.
    /// </summary>
    private void SeedFirstAction()
    {
        Actions.Add(new ActionEntryViewModel(this)
        {
            Name = "Play",
            Path = Executables.FirstOrDefault(),
            WorkingDirectory = InstallDirectoryVariable,
            IsPrimary = true,
        });

        OnPropertyChanged(nameof(HasActions));
    }

    private void PopulateExecutables()
    {
        var candidates = ShowAllExecutables
            ? _allExecutables
            : [.. _allExecutables.Where(IsLikelyGameExecutable)];

        // Never hide everything: if the filter leaves nothing, show the unfiltered list.
        if (candidates.Count == 0)
            candidates = _allExecutables;

        Executables.Clear();

        foreach (var executable in candidates)
            Executables.Add(executable);
    }

    [RelayCommand]
    private void AddAction()
    {
        Actions.Add(new ActionEntryViewModel(this)
        {
            Name = $"Action {Actions.Count + 1}",
            WorkingDirectory = InstallDirectoryVariable,
            IsPrimary = Actions.Count == 0,
        });

        OnPropertyChanged(nameof(HasActions));

        UpdateSummary();
    }

    internal void MoveUp(ActionEntryViewModel action)
    {
        var index = Actions.IndexOf(action);

        if (index > 0)
            Actions.Move(index, index - 1);
    }

    internal void MoveDown(ActionEntryViewModel action)
    {
        var index = Actions.IndexOf(action);

        if (index >= 0 && index < Actions.Count - 1)
            Actions.Move(index, index + 1);
    }

    internal void Remove(ActionEntryViewModel action)
    {
        Actions.Remove(action);

        EnsurePrimary();

        OnPropertyChanged(nameof(HasActions));

        UpdateSummary();
    }

    /// <summary>
    /// Keeps the primary flag behaving like a radio button.
    /// </summary>
    /// <remarks>
    /// The launcher falls back to the first action when none is marked, so a list with no
    /// primary still launches something — just not necessarily what the user meant. Keeping
    /// exactly one means the Play button does what they pointed at.
    /// </remarks>
    internal void OnPrimaryChanged(ActionEntryViewModel action, bool isPrimary)
    {
        if (_syncingPrimary)
            return;

        _syncingPrimary = true;

        try
        {
            if (isPrimary)
            {
                foreach (var other in Actions.Where(a => a != action))
                    other.IsPrimary = false;
            }
            else if (!Actions.Any(a => a.IsPrimary))
            {
                // Unticking the only primary would leave none, so it goes straight back. The way
                // to change which action is primary is to tick a different one.
                action.IsPrimary = true;
            }
        }
        finally
        {
            _syncingPrimary = false;
        }

        UpdateSummary();
    }

    private void EnsurePrimary()
    {
        if (Actions.Count == 0 || Actions.Any(a => a.IsPrimary))
            return;

        _syncingPrimary = true;

        try
        {
            Actions[0].IsPrimary = true;
        }
        finally
        {
            _syncingPrimary = false;
        }
    }

    /// <summary>
    /// Launches an action as configured, against the game as it sits on disk right now.
    /// </summary>
    /// <remarks>
    /// Only <c>{InstallDir}</c> is expanded. The runtime substitutes a wider set at launch —
    /// display metrics, the server address, custom fields — but those come from a live session
    /// that does not exist here, and quietly leaving them unexpanded is more honest than
    /// inventing values the player's machine will not use.
    /// </remarks>
    internal void Test(ActionEntryViewModel action)
    {
        var installDirectory = Package.InstallDirectory;

        if (string.IsNullOrWhiteSpace(action.Path))
            return;

        var executable = Path.GetFullPath(Path.Combine(
            installDirectory,
            action.Path.Trim().Replace('/', Path.DirectorySeparatorChar)));

        if (!File.Exists(executable))
        {
            TestStatus = $"Could not find {executable}.";

            return;
        }

        var workingDirectory = Expand(action.WorkingDirectory, installDirectory);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = Expand(action.Arguments, installDirectory),
                WorkingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : installDirectory,
                UseShellExecute = true,
            });

            // Nothing on success: the game appearing is the confirmation. The status line is
            // only there for the cases where it does not.
            TestStatus = string.Empty;
        }
        catch (Exception ex)
        {
            TestStatus = $"Could not start {Path.GetFileName(executable)}: {ex.Message}";
        }
    }

    private static string Expand(string? value, string installDirectory) =>
        (value ?? string.Empty).Replace(InstallDirectoryVariable, installDirectory);

    /// <summary>True when the path points at a file the package will actually contain.</summary>
    internal bool IsPackagedFile(string? relativePath) =>
        !string.IsNullOrWhiteSpace(relativePath) &&
        _packagedFiles.Contains(relativePath.Trim().Replace('\\', '/'));

    /// <summary>Called by a row whenever an edit could change whether the step is complete.</summary>
    internal void NotifyActionsChanged() => UpdateSummary();

    private void UpdateSummary()
    {
        if (Actions.Count == 0)
        {
            Summary = "Add at least one action so the launcher knows how to start the game.";
            CanGoNext = false;

            return;
        }

        var incomplete = Actions.Count(a => !a.IsValid);

        if (incomplete > 0)
        {
            Summary = $"{incomplete} action(s) still need a name and a path.";
            CanGoNext = false;

            return;
        }

        var missing = Actions.Count(a => a.IsPathMissing);

        // A path outside the package is a warning, not a blocker: it may point at something
        // installed by a script or a redistributable rather than by the archive.
        Summary = missing > 0
            ? $"{Actions.Count} action(s). {missing} point at a file that is not in the package."
            : $"{Actions.Count} action(s), {_allExecutables.Count} executable(s) found among the selected files.";

        CanGoNext = true;
    }

    private static bool IsLikelyGameExecutable(string relativePath)
    {
        var name = Path.GetFileNameWithoutExtension(relativePath);

        return !NonGameExecutableHints.Any(hint =>
            name.Contains(hint, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Converts an absolute packaged file to the forward slashed relative form the manifest uses.
    /// </summary>
    /// <remarks>
    /// Forward slashes because the launcher rewrites them to the local separator when it expands
    /// the path, and does not do the reverse — a backslash written here arrives on Linux as part
    /// of the filename rather than as a directory boundary.
    /// </remarks>
    private string ToRelativePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(Package.InstallDirectory))
            return absolutePath;

        return Path
            .GetRelativePath(Package.InstallDirectory, absolutePath)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/');
    }

    public override Task OnLeaveAsync()
    {
        // Sort order comes from the list's own order rather than from a stored field, so moving
        // a row up is all it takes to change what the launcher lists first.
        Package.Manifest.Actions = [.. Actions
            .Where(a => a.IsValid)
            .Select((a, index) => new SDK.Models.Manifest.Action
            {
                Name = a.Name.Trim(),
                Path = a.Path!.Trim().Replace('\\', '/'),
                Arguments = a.Arguments,
                WorkingDirectory = a.WorkingDirectory,
                IsPrimaryAction = a.IsPrimary,
                SortOrder = index,

                // Platforms is deliberately left unset. RuntimePlatform.None is what the
                // runtime reads as "no restriction", which is right for a package built from a
                // single install — narrowing it belongs on the server, where a game can carry
                // per-platform builds.
            })];

        return Task.CompletedTask;
    }
}
