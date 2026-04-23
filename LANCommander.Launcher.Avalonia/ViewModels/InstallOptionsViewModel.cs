using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using ByteSizeLib;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.SDK.Enums;

namespace LANCommander.Launcher.Avalonia.ViewModels;

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

    // ── Addons ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddons))]
    private ObservableCollection<InstallAddonItemViewModel> _addons = new();

    public bool HasAddons => Addons.Count > 0;

    // ── Size info ────────────────────────────────────────────────────────────

    /// <summary>Base game compressed archive size in bytes.</summary>
    public long BaseDownloadSize { get; set; }

    /// <summary>Base game uncompressed archive size in bytes.</summary>
    public long BaseSpaceRequired { get; set; }

    public string DownloadSizeText => ByteSize.FromBytes(TotalDownloadSize).ToString("0.##");
    public string SpaceRequiredText => ByteSize.FromBytes(TotalSpaceRequired).ToString("0.##");

    private long TotalDownloadSize =>
        BaseDownloadSize + Addons.Where(a => a.IsSelected).Sum(a => a.DownloadSize);

    private long TotalSpaceRequired =>
        BaseSpaceRequired + Addons.Where(a => a.IsSelected).Sum(a => a.SpaceRequired);

    public void RefreshSizes()
    {
        OnPropertyChanged(nameof(DownloadSizeText));
        OnPropertyChanged(nameof(SpaceRequiredText));
    }

    // ── Result ────────────────────────────────────────────────────────────────

    /// <summary>The addons the user chose to install.</summary>
    public SDK.Models.Game[] SelectedAddons =>
        Addons.Where(a => a.IsSelected).Select(a => a.Game).ToArray();
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
