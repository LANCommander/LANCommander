using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ByteSizeLib;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LANCommander.Launcher.ViewModels;

/// <summary>
/// ViewModel for the Manage dialog's "Saves" section. Lists the game's cloud saves so the user can
/// download or delete them; the host dialog supplies the footer "Upload Current Save" action.
/// </summary>
public partial class GameSavesViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaves))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ObservableCollection<GameSaveItemViewModel> _saves = new();

    public bool HasSaves => Saves.Count > 0;
    public bool IsEmpty => Saves.Count == 0;

    [ObservableProperty]
    private string _totalSizeText = string.Empty;

    /// <summary>
    /// Repopulates the list and refreshes the empty-state flags. Items arrive newest first, so the
    /// first one is flagged as the latest.
    /// </summary>
    public void SetSaves(System.Collections.Generic.IEnumerable<GameSaveItemViewModel> items)
    {
        Saves = new ObservableCollection<GameSaveItemViewModel>(items);

        for (var i = 0; i < Saves.Count; i++)
            Saves[i].IsLatest = i == 0;

        var totalBytes = Saves.Sum(s => s.Save.Size);
        TotalSizeText = totalBytes > 0 ? $"{ByteSize.FromBytes(totalBytes).ToString("0.#")} total" : string.Empty;
    }
}

public partial class GameSaveItemViewModel : ViewModelBase
{
    public SDK.Models.GameSave Save { get; }

    public string CreatedOnText => Save.CreatedOn.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

    /// <summary>Sortable timestamp for the mono column, e.g. 2026-09-22 20:41.</summary>
    public string TimestampText => Save.CreatedOn.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string RelativeText => ToRelative(Now() - Save.CreatedOn.ToLocalTime());

    /// <summary>The current time. Debug fixtures pin it so "3 days ago" stays true.</summary>
    internal static Func<DateTime> Now { get; set; } = () => DateTime.Now;

    /// <summary>Newest save on the server; set by <see cref="GameSavesViewModel.SetSaves"/>.</summary>
    [ObservableProperty]
    private bool _isLatest;

    private static string ToRelative(TimeSpan age)
    {
        if (age < TimeSpan.FromMinutes(1)) return "just now";
        if (age < TimeSpan.FromHours(1)) return Plural((int)age.TotalMinutes, "minute");
        if (age < TimeSpan.FromDays(1)) return Plural((int)age.TotalHours, "hour");
        if (age < TimeSpan.FromDays(2)) return "yesterday";
        if (age < TimeSpan.FromDays(7)) return Plural((int)age.TotalDays, "day");
        if (age < TimeSpan.FromDays(30)) return Plural((int)(age.TotalDays / 7), "week");
        if (age < TimeSpan.FromDays(365)) return Plural((int)(age.TotalDays / 30), "month");
        return Plural((int)(age.TotalDays / 365), "year");

        static string Plural(int n, string unit) => n == 1 ? $"1 {unit} ago" : $"{n} {unit}s ago";
    }

    public string SizeText => Save.Size > 0
        ? ByteSize.FromBytes(Save.Size).ToString("0.##")
        : string.Empty;
    public bool HasSize => Save.Size > 0;

    /// <summary>Disables the row's buttons while a download/delete is running.</summary>
    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Invoked when the user downloads this save. Set by the host dialog.</summary>
    public Func<GameSaveItemViewModel, Task>? DownloadRequested { get; set; }

    /// <summary>Invoked when the user deletes this save. Set by the host dialog.</summary>
    public Func<GameSaveItemViewModel, Task>? DeleteRequested { get; set; }

    public GameSaveItemViewModel(SDK.Models.GameSave save)
    {
        Save = save;
    }

    [RelayCommand]
    private async Task DownloadAsync()
    {
        if (IsBusy || DownloadRequested is null)
            return;

        IsBusy = true;

        try
        {
            await DownloadRequested(this);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (IsBusy || DeleteRequested is null)
            return;

        IsBusy = true;

        try
        {
            await DeleteRequested(this);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
