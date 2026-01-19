using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Data.Models;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels.Components;

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

    public event EventHandler? DepotSelected;
    public event EventHandler<LibraryItemViewModel>? ItemSelected;
    public event EventHandler? RefreshRequested;
    public event EventHandler? LogoutRequested;

    public LibrarySidebarViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<LibrarySidebarViewModel>>();
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

            var items = await libraryService.GetItemsAsync();

            foreach (var item in items ?? [])
            {
                if (item.DataItem is Game game)
                {
                    var iconPath = GetIconPath(game, mediaService);
                    Items.Add(new LibraryItemViewModel(game, iconPath));
                }
            }

            StatusMessage = $"{Items.Count} games";
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

    private string? GetIconPath(Game game, MediaService mediaService)
    {
        var icon = game.Media?.FirstOrDefault(m => m.Type == MediaType.Icon);
        if (icon == null) return null;

        var path = mediaService.GetImagePath(icon);
        return mediaService.FileExists(icon) ? path : null;
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
        IsDepotSelected = false;
        ItemSelected?.Invoke(this, item);
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
            SelectedItem = item;
            IsDepotSelected = false;
        }
    }
}
