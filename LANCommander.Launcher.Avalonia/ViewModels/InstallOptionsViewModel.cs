using System.Collections.ObjectModel;
using System.Linq;
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

    public bool HasMultipleDirectories => InstallDirectories.Count > 1;

    // ── Addons ────────────────────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAddons))]
    private ObservableCollection<InstallAddonItemViewModel> _addons = new();

    public bool HasAddons => Addons.Count > 0;

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

    [ObservableProperty]
    private bool _isSelected;

    public InstallAddonItemViewModel(SDK.Models.Game game, bool selectedByDefault = false)
    {
        Game       = game;
        IsSelected = selectedByDefault;
    }
}
