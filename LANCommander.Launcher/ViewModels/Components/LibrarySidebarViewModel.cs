using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Components;

/// <summary>
/// ViewModel for the library sidebar showing user's games
/// </summary>
public partial class LibrarySidebarViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LibrarySidebarViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<LibraryItemViewModel> _items = new();

    [ObservableProperty]
    private LibraryItemViewModel? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isDepotSelected = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isOfflineMode;

    public event EventHandler? DepotSelected;
    public event EventHandler<LibraryItemViewModel>? ItemSelected;
    public event EventHandler? RefreshRequested;
    public event EventHandler? LogoutRequested;
    public event EventHandler? SettingsRequested;
    public event EventHandler? GoOnlineRequested;
    public event EventHandler? GoOfflineRequested;

    // Prevents OnSelectedItemChanged from firing ItemSelected during programmatic selection
    private bool _suppressItemSelected;

    public LibrarySidebarViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LibrarySidebarViewModel>>();
    }

    partial void OnSelectedItemChanged(LibraryItemViewModel? value)
    {
        if (!_suppressItemSelected && value != null)
        {
            IsDepotSelected = false;
            ItemSelected?.Invoke(this, value);
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        Items.Clear();
        _logger.LogDebug("Loading library items...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var libraryService = scope.ServiceProvider.GetRequiredService<LibraryService>();
            var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
            var mediaClient = scope.ServiceProvider.GetRequiredService<MediaClient>();

            var items = await libraryService.GetItemsAsync();

            foreach (var item in items ?? [])
            {
                if (item.DataItem is Game game)
                {
                    var iconPath = await GetOrDownloadIconPathAsync(game, mediaService, mediaClient);
                    Items.Add(new LibraryItemViewModel(game, iconPath));
                }
            }

            StatusMessage = IsOfflineMode ? $"{Items.Count} games (offline)" : $"{Items.Count} games";
            _logger.LogDebug("Loaded {Count} library items", Items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load library items");
            StatusMessage = "Failed to load";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<string?> GetOrDownloadIconPathAsync(Game game, MediaService mediaService, MediaClient mediaClient)
    {
        var icon = game.Media?.FirstOrDefault(m => m.Type == MediaType.Icon);
        if (icon == null) return null;

        var path = mediaService.GetImagePath(icon);

        if (mediaService.FileExists(icon))
            return path;

        if (IsOfflineMode)
            return null;

        try
        {
            var sdkMedia = new SDK.Models.Media
            {
                Id = icon.Id,
                FileId = icon.FileId,
                Crc32 = icon.Crc32,
                MimeType = icon.MimeType,
                Type = icon.Type,
            };

            var fileInfo = await mediaClient.DownloadAsync(sdkMedia, path);

            if (fileInfo.Exists)
                return fileInfo.FullName;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to download icon for game {GameId}", game.Id);
        }

        return null;
    }

    [RelayCommand]
    private void ShowDepot()
    {
        SelectedItem = null;
        IsDepotSelected = true;
        DepotSelected?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void SelectItem(LibraryItemViewModel? item)
    {
        if (item == null) return;
        SelectedItem = item;
        // OnSelectedItemChanged handles IsDepotSelected and ItemSelected event
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Logout()
    {
        LogoutRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Settings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void GoOnline()
    {
        GoOnlineRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void GoOffline()
    {
        GoOfflineRequested?.Invoke(this, EventArgs.Empty);
    }

    public void ClearSelection()
    {
        SelectedItem = null;
        IsDepotSelected = false;
    }

    public void SelectDepot()
    {
        SelectedItem = null;
        IsDepotSelected = true;
    }

    public void SelectItemById(Guid id)
    {
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item != null)
        {
            _suppressItemSelected = true;
            SelectedItem = item;
            _suppressItemSelected = false;
            IsDepotSelected = false;
        }
    }
}
