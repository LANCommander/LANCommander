using System;
using System.Collections.ObjectModel;
using ByteSizeLib;
using CommunityToolkit.Mvvm.ComponentModel;

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
    private ObservableCollection<GameVersionItemViewModel> _versions = new();
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

    /// <summary>Only versions that carry an archive and aren't already installed can be switched to.</summary>
    public bool IsInstallable => !IsInstalled
        && Version.ArchiveId.HasValue
        && Version.ArchiveId.Value != Guid.Empty;

    /// <summary>Label for the action button: "Update" for a newer version, "Roll Back" for an older one.</summary>
    public string ButtonText { get; }

    public GameVersionItemViewModel(SDK.Models.GameVersion version, bool isInstalled, bool isNewerThanInstalled)
    {
        Version = version;
        IsInstalled = isInstalled;
        ButtonText = isNewerThanInstalled ? "Update" : "Roll Back";
    }
}
