using System;
using System.Collections.ObjectModel;
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

    /// <summary>Repopulates the list and refreshes the empty-state flags.</summary>
    public void SetSaves(System.Collections.Generic.IEnumerable<GameSaveItemViewModel> items)
    {
        Saves = new ObservableCollection<GameSaveItemViewModel>(items);
    }
}

public partial class GameSaveItemViewModel : ViewModelBase
{
    public SDK.Models.GameSave Save { get; }

    public string CreatedOnText => Save.CreatedOn.ToLocalTime().ToString("MMM d, yyyy h:mm tt");

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
