using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// One row of the action editor — a single entry point the launcher can start the game with.
/// </summary>
/// <remarks>
/// Holds a reference back to its step so the row template can bind commands and the shared
/// executable list directly, rather than reaching up through the visual tree for them.
/// </remarks>
public partial class ActionEntryViewModel : ViewModelBase
{
    private readonly ActionStepViewModel _step;

    public ActionEntryViewModel(ActionStepViewModel step)
    {
        _step = step;
    }

    /// <summary>Executables found among the package's files, shared by every row.</summary>
    public ObservableCollection<string> AvailablePaths => _step.Executables;

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Path to the executable, relative to the install directory and forward slashed.
    /// </summary>
    /// <remarks>
    /// Relative because that is what the manifest carries: the launcher expands the archive
    /// somewhere of its own choosing on the player's machine, so an absolute path from the
    /// packaging machine would be meaningless there.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPathMissing))]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    private string? _path;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = ActionStepViewModel.InstallDirectoryVariable;

    /// <summary>
    /// The action the launcher runs when the user just presses Play.
    /// </summary>
    /// <remarks>
    /// Behaves like a radio button across the list even though it is drawn as a checkbox:
    /// exactly one action is primary, and the step re-points it rather than allowing none.
    /// </remarks>
    [ObservableProperty]
    private bool _isPrimary;

    /// <summary>
    /// True when the path does not match any file going into the package.
    /// </summary>
    /// <remarks>
    /// Surfaced rather than enforced: a path can legitimately point at something the file step
    /// did not offer. What it catches is the common case — going back, changing the file
    /// selection, and leaving an action aimed at a file that is no longer included.
    /// </remarks>
    public bool IsPathMissing =>
        !string.IsNullOrWhiteSpace(Path) && !_step.IsPackagedFile(Path);

    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Path);

    partial void OnNameChanged(string value) => _step.NotifyActionsChanged();

    partial void OnPathChanged(string? value) => _step.NotifyActionsChanged();

    partial void OnIsPrimaryChanged(bool value) => _step.OnPrimaryChanged(this, value);

    /// <summary>Re-evaluates state that depends on the step rather than on this row.</summary>
    public void RefreshDerivedState()
    {
        OnPropertyChanged(nameof(IsPathMissing));

        TestCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Runs the action against the freshly installed game, so the path and arguments can be
    /// checked before the package is built rather than after someone installs it.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTest))]
    private void Test() => _step.Test(this);

    private bool CanTest() => !string.IsNullOrWhiteSpace(Path);

    [RelayCommand]
    private void MoveUp() => _step.MoveUp(this);

    [RelayCommand]
    private void MoveDown() => _step.MoveDown(this);

    [RelayCommand]
    private void Remove() => _step.Remove(this);
}
