using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
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
    [NotifyPropertyChangedFor(nameof(FreeSpaceText))]
    [NotifyPropertyChangedFor(nameof(HasFreeSpaceInfo))]
    [NotifyPropertyChangedFor(nameof(HasEnoughSpace))]
    [NotifyPropertyChangedFor(nameof(SpaceVerdictText))]
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

    /// <summary>
    /// Changing an existing install (Manage → Modify) rather than a fresh one. The base game is
    /// already on disk, so its row and the download total don't apply.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTotal))]
    private bool _isModify;

    // ── Free space on the chosen drive ────────────────────────────────────────

    /// <summary>
    /// Replaces the drive lookup when set. Debug fixtures use it so the free-space line shows the same
    /// numbers on every machine.
    /// </summary>
    internal static Func<string?, (string Name, long AvailableFreeSpace)?>? FreeSpaceOverride { get; set; }

    private (string Name, long AvailableFreeSpace)? SelectedDrive
    {
        get
        {
            if (FreeSpaceOverride != null)
                return FreeSpaceOverride(SelectedInstallDirectory);

            try
            {
                var root = Path.GetPathRoot(SelectedInstallDirectory);
                return string.IsNullOrEmpty(root) ? null : new DriveInfo(root) is { IsReady: true } drive ? (drive.Name, drive.AvailableFreeSpace) : null;
            }
            catch
            {
                return null;
            }
        }
    }

    public bool HasFreeSpaceInfo => SelectedDrive != null;

    /// <summary>e.g. "218 GB free on D:".</summary>
    public string FreeSpaceText => SelectedDrive is { } drive
        ? $"{ByteSize.FromBytes(drive.AvailableFreeSpace).ToString("0.#")} free on {drive.Name.TrimEnd('\\', '/')}"
        : string.Empty;

    public bool HasEnoughSpace => SelectedDrive is not { } drive || drive.AvailableFreeSpace >= TotalSpaceRequired;

    public string SpaceVerdictText => HasEnoughSpace ? "enough room" : "not enough room";

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

    /// <summary>Base game compressed archive size in bytes.</summary>
    public long BaseDownloadSize { get; set; }

    /// <summary>Base game uncompressed archive size in bytes.</summary>
    public long BaseSpaceRequired { get; set; }

    public string DownloadSizeText => ByteSize.FromBytes(TotalDownloadSize).ToString("0.##");
    public string SpaceRequiredText => ByteSize.FromBytes(TotalSpaceRequired).ToString("0.##");
    public string BaseDownloadSizeText => BaseDownloadSize > 0 ? ByteSize.FromBytes(BaseDownloadSize).ToString("0.##") : string.Empty;

    public bool HasSizeInfo => HasAddons || HasTools;

    /// <summary>The download total is only meaningful for a fresh install with something to choose.</summary>
    public bool ShowTotal => HasSizeInfo && !IsModify;

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
        OnPropertyChanged(nameof(BaseDownloadSizeText));
        OnPropertyChanged(nameof(HasEnoughSpace));
        OnPropertyChanged(nameof(SpaceVerdictText));
    }

    // ── Result ────────────────────────────────────────────────────────────────

    /// <summary>The addons the user chose to install.</summary>
    public SDK.Models.Game[] SelectedAddons =>
        Addons.Where(a => a.IsSelected).Select(a => a.Game).ToArray();

    /// <summary>The tools the user chose to install.</summary>
    public SDK.Models.Tool[] SelectedTools =>
        Tools.Where(t => t.IsSelected).Select(t => t.Tool).ToArray();
}

public partial class InstallToolItemViewModel : ViewModelBase
{
    public SDK.Models.Tool Tool { get; }

    public string Title => Tool.Name ?? "Unknown";

    public long DownloadSize { get; }
    public long SpaceRequired { get; }
    public string SizeText => DownloadSize > 0 ? ByteSize.FromBytes(DownloadSize).ToString("0.##") : string.Empty;

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
    public string SizeText => DownloadSize > 0 ? ByteSize.FromBytes(DownloadSize).ToString("0.##") : string.Empty;

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
