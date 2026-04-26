using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Avalonia.Services;
using LANCommander.Launcher.Avalonia.ViewModels.Components;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Enums;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class GameDetailViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly INavigationService _navigationService;
    private readonly ILogger<GameDetailViewModel> _logger;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string? _coverPath;

    [ObservableProperty]
    private string? _logoPath;

    [ObservableProperty]
    private string? _backgroundPath;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private DateTime _releasedOn;

    [ObservableProperty]
    private string _releaseYear = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayerInfo))]
    private bool _singleplayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenreList))]
    private string _genres = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeveloperList))]
    private string _developers = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PublisherList))]
    private string _publishers = string.Empty;

    [ObservableProperty]
    private string _platforms = string.Empty;

    [ObservableProperty]
    private string _multiplayerModes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TagList))]
    [NotifyPropertyChangedFor(nameof(VisibleTagList))]
    [NotifyPropertyChangedFor(nameof(HasMoreTags))]
    [NotifyPropertyChangedFor(nameof(ExtraTagCount))]
    [NotifyPropertyChangedFor(nameof(ShowMoreTagsLabel))]
    private string _tags = string.Empty;

    // ── Tags expand/collapse ─────────────���────────────────────────────────────

    private const int TagsVisibleLimit = 5;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VisibleTagList))]
    [NotifyPropertyChangedFor(nameof(HasMoreTags))]
    [NotifyPropertyChangedFor(nameof(ExtraTagCount))]
    [NotifyPropertyChangedFor(nameof(ShowMoreTagsLabel))]
    private bool _tagsExpanded;

    public IEnumerable<string> VisibleTagList =>
        TagsExpanded ? TagList : TagList.Take(TagsVisibleLimit);

    public bool HasMoreTags    => TagList.Count() > TagsVisibleLimit;
    public int  ExtraTagCount  => Math.Max(0, TagList.Count() - TagsVisibleLimit);
    public string ShowMoreTagsLabel =>
        TagsExpanded ? "Show less" : $"+{ExtraTagCount} more";

    [RelayCommand]
    private void ToggleTagsExpanded() => TagsExpanded = !TagsExpanded;

    // ── Screenshots / videos ──────────────────────���────────────────────────���──

    public ObservableCollection<GameMediaItemViewModel> MediaItems { get; } = new();

    public bool HasMedia => MediaItems.Count > 0;

    // ── Multiplayer modes ─────────────────────────────────────────────────────

    public ObservableCollection<string> MultiplayerModeDetails { get; } = new();

    // ── Other ─────────────────────────���───────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BackLabel))]
    private bool _fromLibrary;

    public string BackLabel => FromLibrary ? "Back to Library" : "Back to Depot";

    // Split list properties for chip rendering
    public IEnumerable<string> GenreList     => SplitCsv(Genres);
    public IEnumerable<string> DeveloperList => SplitCsv(Developers);
    public IEnumerable<string> PublisherList => SplitCsv(Publishers);
    public IEnumerable<string> TagList       => SplitCsv(Tags);

    private static IEnumerable<string> SplitCsv(string csv) =>
        csv.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayerInfo))]
    private bool _hasMultiplayer;

    public bool HasPlayerInfo => Singleplayer || HasMultiplayer;

    [ObservableProperty]
    private bool _isLoadingMedia;

    private bool _isOfflineMode;
    public bool IsOfflineMode
    {
        get => _isOfflineMode;
        set
        {
            if (SetProperty(ref _isOfflineMode, value))
            {
                ActionBar.IsOfflineMode = value;
            }
        }
    }

    // Action bar component
    public GameActionBarViewModel ActionBar { get; }

    public event EventHandler? LibraryChanged;
    public event EventHandler? InstallRequested;
    public event EventHandler<string>? SearchRequested;

    public GameDetailViewModel(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<GameDetailViewModel>>();
        _navigationService = serviceProvider.GetRequiredService<INavigationService>();
        
        // Create action bar and wire up events
        ActionBar = new GameActionBarViewModel(serviceProvider);
        ActionBar.LibraryChanged += (_, _) => LibraryChanged?.Invoke(this, EventArgs.Empty);
        ActionBar.InstallRequested += (_, _) => InstallRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Refreshes the install status from the database.
    /// Called after an installation completes.
    /// </summary>
    public async Task RefreshInstallStatusAsync()
    {
        if (Id == Guid.Empty) return;
        await ActionBar.RefreshAsync();
    }

    /// <summary>
    /// Load game from local cache (Data.Models.Game)
    /// Used when selecting from the library sidebar
    /// </summary>
    public async void LoadGame(Data.Models.Game game)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        ReleasedOn = game.ReleasedOn ?? DateTime.MinValue;
        ReleaseYear = game.ReleasedOn?.Year > 1 ? game.ReleasedOn.Value.Year.ToString() : "Unknown";
        Singleplayer = game.Singleplayer;

        // Get media paths from local storage
        using var scope = _serviceProvider.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<MediaService>();
        
        CoverPath = GetLocalMediaPath(game.Media, MediaType.Cover, mediaService);
        LogoPath = GetLocalMediaPath(game.Media, MediaType.Logo, mediaService);
        BackgroundPath = GetLocalMediaPath(game.Media, MediaType.Background, mediaService);
        IconPath = GetLocalMediaPath(game.Media, MediaType.Icon, mediaService);

        // Collections
        Genres = game.Genres != null 
            ? string.Join(", ", game.Genres.Select(g => g.Name)) 
            : string.Empty;

        Developers = game.Developers != null 
            ? string.Join(", ", game.Developers.Select(d => d.Name)) 
            : string.Empty;

        Publishers = game.Publishers != null 
            ? string.Join(", ", game.Publishers.Select(p => p.Name)) 
            : string.Empty;

        Platforms = game.Platforms != null 
            ? string.Join(", ", game.Platforms.Select(p => p.Name)) 
            : string.Empty;

        Tags = game.Tags != null 
            ? string.Join(", ", game.Tags.Select(t => t.Name)) 
            : string.Empty;

        // Multiplayer info
        HasMultiplayer = game.MultiplayerModes != null && game.MultiplayerModes.Any();
        MultiplayerModeDetails.Clear();
        if (HasMultiplayer)
        {
            var modes = game.MultiplayerModes!
                .Select(m => m.Type.ToString())
                .Distinct();
            MultiplayerModes = string.Join(", ", modes);
            foreach (var mode in game.MultiplayerModes!)
                MultiplayerModeDetails.Add(FormatMultiplayerMode(mode));
        }
        else
        {
            MultiplayerModes = string.Empty;
        }

        // Media items (screenshots / videos from local cache)
        MediaItems.Clear();
        TagsExpanded = false;
        if (game.Media != null)
        {
            foreach (var m in game.Media.Where(m =>
                m.Type == MediaType.Screenshot || m.Type == MediaType.Video))
            {
                var path = mediaService.FileExists(m) ? mediaService.GetImagePath(m) : null;
                if (path == null) continue;

                var item = new GameMediaItemViewModel
                {
                    Path     = path,
                    IsVideo  = m.Type == MediaType.Video,
                    MimeType = string.Empty
                };

                // Pre-load bitmap for screenshots so the AXAML can bind to ImageSource
                if (!item.IsVideo)
                {
                    try
                    {
                        item.ImageSource = new Bitmap(path);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load local screenshot {Path}", path);
                        continue;
                    }
                }

                MediaItems.Add(item);
            }
        }
        OnPropertyChanged(nameof(HasMedia));

        // Load action bar state
        await ActionBar.LoadFromLocalGameAsync(game);
    }

    /// <summary>
    /// Load game from server API (SDK.Models.Game)
    /// Used when selecting from the depot/all games list
    /// </summary>
    public async Task LoadGameAsync(SDK.Models.Game game)
    {
        Id = game.Id;
        Title = game.Title ?? "Unknown";
        Description = game.Description ?? string.Empty;
        ReleasedOn = game.ReleasedOn;
        ReleaseYear = game.ReleasedOn.Year > 1 ? game.ReleasedOn.Year.ToString() : "Unknown";
        Singleplayer = game.Singleplayer;

        // Reset media paths while loading
        CoverPath = null;
        LogoPath = null;
        BackgroundPath = null;
        IconPath = null;

        // Collections
        Genres = game.Genres != null 
            ? string.Join(", ", game.Genres.Select(g => g.Name)) 
            : string.Empty;

        Developers = game.Developers != null 
            ? string.Join(", ", game.Developers.Select(d => d.Name)) 
            : string.Empty;

        Publishers = game.Publishers != null 
            ? string.Join(", ", game.Publishers.Select(p => p.Name)) 
            : string.Empty;

        Platforms = game.Platforms != null 
            ? string.Join(", ", game.Platforms.Select(p => p.Name))
            : string.Empty;

        Tags = game.Tags != null 
            ? string.Join(", ", game.Tags.Select(t => t.Name)) 
            : string.Empty;

        // Multiplayer info
        HasMultiplayer = game.MultiplayerModes != null && game.MultiplayerModes.Any();
        MultiplayerModeDetails.Clear();
        if (HasMultiplayer)
        {
            var modes = game.MultiplayerModes!
                .Select(m => m.Type.ToString())
                .Distinct();
            MultiplayerModes = string.Join(", ", modes);
            foreach (var mode in game.MultiplayerModes!)
                MultiplayerModeDetails.Add(FormatMultiplayerMode(mode));
        }
        else
        {
            MultiplayerModes = string.Empty;
        }

        // Reset media items and tags state while we re-load
        MediaItems.Clear();
        TagsExpanded = false;
        OnPropertyChanged(nameof(HasMedia));

        // Load action bar state
        await ActionBar.LoadFromSdkGameAsync(game);

        // Load media asynchronously — stream from server, don't save to disk
        if (game.Media != null && game.Media.Any())
        {
            IsLoadingMedia = true;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var mediaClient = scope.ServiceProvider.GetRequiredService<MediaClient>();

                CoverPath      = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Cover,       mediaClient);
                LogoPath       = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Logo,        mediaClient);
                BackgroundPath = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Background,  mediaClient);
                IconPath       = await GetOrDownloadMediaPathAsync(game.Media, MediaType.Icon,        mediaClient);

                // Screenshots and videos — load from server without saving to disk
                using var httpClient = new HttpClient();
                foreach (var media in game.Media.Where(m =>
                    m.Type == MediaType.Screenshot || m.Type == MediaType.Video))
                {
                    try
                    {
                        var url = mediaClient.GetAbsoluteUrl(media);
                        var item = new GameMediaItemViewModel
                        {
                            IsVideo  = media.Type == MediaType.Video,
                            MimeType = media.MimeType ?? string.Empty
                        };

                        if (media.Type == MediaType.Video)
                        {
                            // Videos: use streaming endpoint with range request support
                            item.Path = mediaClient.GetAbsoluteStreamUrl(media);
                        }
                        else
                        {
                            // Screenshots: fetch bytes into memory and create Bitmap
                            var bytes = await httpClient.GetByteArrayAsync(url);
                            using var ms = new MemoryStream(bytes);
                            item.ImageSource = new Bitmap(ms);
                        }

                        MediaItems.Add(item);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load media {MediaId} from server", media.Id);
                    }
                }
                OnPropertyChanged(nameof(HasMedia));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load media for game {GameId}", game.Id);
            }
            finally
            {
                IsLoadingMedia = false;
            }
        }
    }

    private static string FormatMultiplayerMode(Data.Models.MultiplayerMode mode) =>
        FormatMultiplayerMode(mode.Type, mode.MinPlayers, mode.MaxPlayers);

    private static string FormatMultiplayerMode(SDK.Models.MultiplayerMode mode) =>
        FormatMultiplayerMode(mode.Type, mode.MinPlayers, mode.MaxPlayers);

    private static string FormatMultiplayerMode(SDK.Enums.MultiplayerType type, int minPlayers, int maxPlayers)
    {
        var typeLabel = type switch
        {
            SDK.Enums.MultiplayerType.Local  => "Local Multiplayer",
            SDK.Enums.MultiplayerType.LAN    => "LAN Multiplayer",
            SDK.Enums.MultiplayerType.Online => "Online Multiplayer",
            _                                => type.ToString()
        };

        if (maxPlayers > 0)
        {
            var range = minPlayers > 1 && minPlayers < maxPlayers
                ? $"{minPlayers}–{maxPlayers} players"
                : $"Up to {maxPlayers} players";
            return $"{typeLabel} · {range}";
        }

        return typeLabel;
    }

    private string? GetLocalMediaPath(System.Collections.Generic.ICollection<Data.Models.Media>? mediaCollection, MediaType type, MediaService mediaService)
    {
        var media = mediaCollection?.FirstOrDefault(m => m.Type == type);
        if (media == null) return null;
        
        var path = mediaService.GetImagePath(media);
        return mediaService.FileExists(media) ? path : null;
    }

    private async Task<string?> GetOrDownloadMediaPathAsync(System.Collections.Generic.IEnumerable<SDK.Models.Media> mediaCollection, MediaType type, MediaClient mediaClient)
    {
        var media = mediaCollection.FirstOrDefault(m => m.Type == type);
        if (media == null) return null;

        try
        {
            var localPath = mediaClient.GetLocalPath(media);
            
            // Check if file exists locally
            if (File.Exists(localPath))
            {
                return localPath;
            }

            // Download the media
            _logger.LogDebug("Downloading media {MediaId} of type {Type}", media.Id, type);
            var fileInfo = await mediaClient.DownloadAsync(media, localPath);
            
            if (fileInfo.Exists)
            {
                return fileInfo.FullName;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get or download media {MediaId}", media.Id);
        }

        return null;
    }

    [RelayCommand]
    private void GoBack()
    {
        ActionBar.StopRunningCheck();
        _navigationService.GoBack();
    }

    [RelayCommand]
    private void SearchFor(string term)
    {
        if (!string.IsNullOrWhiteSpace(term))
            SearchRequested?.Invoke(this, term);
    }
}
