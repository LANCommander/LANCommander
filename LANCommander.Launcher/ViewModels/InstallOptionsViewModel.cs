using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ByteSizeLib;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.ViewModels;

public partial class InstallOptionsViewModel : ViewModelBase
{
    // ── Install directory ──────────────────────────────────────────────────────

    [ObservableProperty]
    private ObservableCollection<string> _installDirectories = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultipleDirectories))]
    private string _selectedInstallDirectory = string.Empty;

    [ObservableProperty]
    private string _gameTitle = string.Empty;

    /// <summary>Title shown at the top of the dialog (e.g. "Install GameTitle" or "Modify GameTitle").</summary>
    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    /// <summary>Label for the confirm button (e.g. "Install" or "Apply").</summary>
    [ObservableProperty]
    private string _confirmButtonText = "Install";

    /// <summary>When true, always show the install directory picker (e.g. for Modify).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowInstallDirectory))]
    private bool _alwaysShowDirectory;

    public bool HasMultipleDirectories => InstallDirectories.Count > 1;
    public bool ShowInstallDirectory => AlwaysShowDirectory || HasMultipleDirectories;

    // ── Base-game version (archive) selector ────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowVersionSelector))]
    [NotifyPropertyChangedFor(nameof(HasSizeInfo))]
    private ObservableCollection<InstallArchiveItemViewModel> _archives = new();

    [ObservableProperty]
    private InstallArchiveItemViewModel? _selectedArchive;

    /// <summary>Only meaningful to show a version picker when there's an actual choice.</summary>
    public bool ShowVersionSelector => Archives.Count > 1;

    /// <summary>The archive id to install, or null when no archive is known (falls back to the
    /// server's own default-resolution behavior).</summary>
    public Guid? SelectedArchiveId => SelectedArchive?.Id;

    /// <summary>
    /// Populates the base-game version selector from the server's full archive list and
    /// preselects the effective default — the explicit admin default if one is set, otherwise
    /// the newest archive by upload date — exactly mirroring <c>SDK.Models.Archive.IsEffectiveDefault</c>
    /// rather than re-deriving "latest" locally. Also seeds the base download/space-required
    /// sizes from that single preselected archive (never a sum across every historical archive).
    /// </summary>
    public void PopulateArchives(IEnumerable<SDK.Models.Archive> archives)
    {
        Archives.Clear();

        foreach (var archive in (archives ?? []).OrderByDescending(a => a.CreatedOn))
            Archives.Add(new InstallArchiveItemViewModel(archive));

        SelectedArchive = Archives.FirstOrDefault(a => a.IsEffectiveDefault) ?? Archives.FirstOrDefault();
    }
    partial void OnSelectedArchiveChanged(InstallArchiveItemViewModel? value)
    {
        BaseDownloadSize  = value?.CompressedSize ?? 0;
        BaseSpaceRequired = value?.UncompressedSize ?? 0;

        RefreshSizes();
    }

    // ── Addons ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddons))]
    private ObservableCollection<InstallAddonItemViewModel> _addons = new();

    public bool HasAddons => Addons.Count > 0;

    // ── Tools ─────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTools))]
    private ObservableCollection<InstallToolItemViewModel> _tools = new();

    public bool HasTools => Tools.Count > 0;

    // ── Size info ────────────────────────────────────────────────────────────

    /// <summary>Base game compressed archive size in bytes (from the selected archive only).</summary>
    public long BaseDownloadSize { get; set; }

    /// <summary>Base game uncompressed archive size in bytes (from the selected archive only).</summary>
    public long BaseSpaceRequired { get; set; }

    public string DownloadSizeText => ByteSize.FromBytes(TotalDownloadSize).ToString("0.##");
    public string SpaceRequiredText => ByteSize.FromBytes(TotalSpaceRequired).ToString("0.##");

    public bool HasSizeInfo => HasAddons || HasTools || Archives.Count > 0;

    private long TotalDownloadSize =>
        BaseDownloadSize
        + Addons.Where(a => a.IsSelected).Sum(a => a.DownloadSize)
        + Tools.Where(t => t.IsSelected).Sum(t => t.DownloadSize);

    private long TotalSpaceRequired =>
        BaseSpaceRequired
        + Addons.Where(a => a.IsSelected).Sum(a => a.SpaceRequired)
        + Tools.Where(t => t.IsSelected).Sum(t => t.SpaceRequired);

    public void RefreshSizes()
    {
        OnPropertyChanged(nameof(DownloadSizeText));
        OnPropertyChanged(nameof(SpaceRequiredText));
    }

    // ── Result ────────────────────────────────────────────────────────────────

    /// <summary>The addons the user chose to install.</summary>
    public SDK.Models.Game[] SelectedAddons =>
        Addons.Where(a => a.IsSelected).Select(a => a.Game).ToArray();

    /// <summary>The tools the user chose to install.</summary>
    public SDK.Models.Tool[] SelectedTools =>
        Tools.Where(t => t.IsSelected).Select(t => t.Tool).ToArray();
}

/// <summary>
/// One selectable base-game version (server archive) shown in the install dialog's version
/// picker: version label, upload date, compressed/uncompressed sizes, changelog, and
/// default/effective-default badge state. Mirrors <see cref="SDK.Models.Archive.IsDefault"/> and
/// <see cref="SDK.Models.Archive.IsEffectiveDefault"/> exactly — the launcher never re-derives
/// "latest"/"default" locally.
/// </summary>
public class InstallArchiveItemViewModel
{
    public Guid Id { get; }
    public string Version { get; }
    public DateTime CreatedOn { get; }
    public long CompressedSize { get; }
    public long UncompressedSize { get; }
    public string? Changelog { get; }
    public bool IsDefault { get; }
    public bool IsEffectiveDefault { get; }

    public bool HasChangelog => !string.IsNullOrWhiteSpace(Changelog);

    /// <summary>"Default" when an admin explicitly pinned this archive, "Latest" when it's only
    /// the effective default via the newest-by-date fallback, empty otherwise.</summary>
    public string BadgeText => IsDefault ? "Default" : IsEffectiveDefault ? "Latest" : string.Empty;
    public bool HasBadge => !string.IsNullOrEmpty(BadgeText);

    public string DisplaySize =>
        $"{ByteSize.FromBytes(CompressedSize).ToString("0.##")} / {ByteSize.FromBytes(UncompressedSize).ToString("0.##")}";

    public string DisplayDate => CreatedOn.ToLocalTime().ToString("MMM d, yyyy");

    public string DetailSummary => $"{DisplayDate} • {DisplaySize}";

    public InstallArchiveItemViewModel(SDK.Models.Archive archive)
    {
        Id = archive.Id;
        Version = string.IsNullOrWhiteSpace(archive.Version) ? "Unknown version" : archive.Version;
        CreatedOn = archive.CreatedOn;
        CompressedSize = archive.CompressedSize;
        UncompressedSize = archive.UncompressedSize;
        Changelog = archive.Changelog;
        IsDefault = archive.IsDefault;
        IsEffectiveDefault = archive.IsEffectiveDefault;
    }
}

public partial class InstallToolItemViewModel : ViewModelBase
{
    public SDK.Models.Tool Tool { get; }

    public string Title => Tool.Name ?? "Unknown";

    public long DownloadSize { get; }
    public long SpaceRequired { get; }

    [ObservableProperty]
    private bool _isSelected;

    public InstallToolItemViewModel(SDK.Models.Tool tool, bool selectedByDefault = false)
    {
        Tool = tool;
        IsSelected = selectedByDefault;

        var archives = tool.Archives?.ToArray() ?? [];
        DownloadSize = archives.Sum(a => a.CompressedSize);
        SpaceRequired = archives.Sum(a => a.UncompressedSize);
    }
}

public partial class InstallAddonItemViewModel : ViewModelBase
{
    public SDK.Models.Game Game { get; }

    public string Title => Game.Title ?? "Unknown";

    public string TypeLabel => Game.Type switch
    {
        GameType.Expansion => "Expansion",
        GameType.Mod       => "Mod",
        _                  => Game.Type.ToString()
    };

    public int TypeSortOrder => Game.Type switch
    {
        GameType.Expansion => 0,
        GameType.Mod       => 1,
        _                  => 2
    };

    public long DownloadSize { get; }
    public long SpaceRequired { get; }

    [ObservableProperty]
    private bool _isSelected;

    public InstallAddonItemViewModel(SDK.Models.Game game, bool selectedByDefault = false)
    {
        Game       = game;
        IsSelected = selectedByDefault;

        var archives = game.Archives?.ToArray() ?? [];
        DownloadSize  = archives.Sum(a => a.CompressedSize);
        SpaceRequired = archives.Sum(a => a.UncompressedSize);
    }
}
