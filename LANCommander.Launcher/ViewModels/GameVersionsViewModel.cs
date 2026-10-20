using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using ByteSizeLib;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// ViewModel for the version picker overlay. Lists every downloadable version for a game so the
/// user can install or roll back to a specific one, along with its changelog and download size.
/// </summary>
public partial class GameVersionsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVersions))]
    private ObservableCollection<GameVersionItemViewModel> _versions = new();

    public bool HasVersions => Versions.Count > 0;

    /// <summary>Whether the game takes updates as soon as they're found. Off keeps it on its current version.</summary>
    [ObservableProperty]
    private bool _automaticallyUpdate;

    /// <summary>Invoked when the user toggles <see cref="AutomaticallyUpdate"/>. Set by the host dialog.</summary>
    public Func<bool, Task>? AutomaticallyUpdateChangedAsync { get; set; }

    partial void OnAutomaticallyUpdateChanged(bool value)
    {
        if (AutomaticallyUpdateChangedAsync is not null)
            _ = AutomaticallyUpdateChangedAsync(value);
    }
}

public partial class GameVersionItemViewModel : ViewModelBase
{
    public SDK.Models.GameVersion Version { get; }

    public string VersionLabel => string.IsNullOrWhiteSpace(Version.Version) ? "(unversioned)" : Version.Version;

    public string ChangelogText => Version.Changelog ?? string.Empty;
    public bool HasChangelog => !string.IsNullOrWhiteSpace(Version.Changelog);

    public string SizeText => Version.CompressedSize > 0
        ? ByteSize.FromBytes(Version.CompressedSize).ToString("0.##")
        : string.Empty;
    public bool HasSize => Version.CompressedSize > 0;

    public bool CreatedOnKnown => Version.CreatedOn != default;
    public string CreatedOnText => Version.CreatedOn.ToLocalTime().ToString("MMM d, yyyy");

    /// <summary>True when this version matches the game's currently installed version.</summary>
    public bool IsInstalled { get; }

    /// <summary>
    /// Versions that aren't installed can be switched to when they have files: their own archive, or for a
    /// config-only version the newest archive below it.
    /// </summary>
    public bool IsInstallable => !IsInstalled
        && (Version.EffectiveArchiveId ?? Version.ArchiveId) is Guid archiveId
        && archiveId != Guid.Empty;

    /// <summary>Label for the action button: "Update" for a newer version, "Roll Back" for an older one.</summary>
    public string ButtonText { get; }

    /// <summary>Invoked when the user picks this version to switch to. Set by the host dialog.</summary>
    public Func<GameVersionItemViewModel, Task>? SwitchRequested { get; set; }

    public GameVersionItemViewModel(SDK.Models.GameVersion version, bool isInstalled, bool isNewerThanInstalled)
    {
        Version = version;
        IsInstalled = isInstalled;
        ButtonText = isNewerThanInstalled ? "Update" : "Roll Back";
    }

    [RelayCommand]
    private async Task SwitchAsync()
    {
        if (SwitchRequested is not null)
            await SwitchRequested(this);
    }
}
