using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LANCommander.Launcher.ViewModels.Packaging;

/// <summary>
/// Describes the game, optionally by looking it up through the server's metadata providers.
/// </summary>
public partial class MetadataStepViewModel : PackagingStepViewModel
{
    private readonly MetadataClient _metadataClient;
    private readonly ILogger<MetadataStepViewModel> _logger;

    public MetadataStepViewModel(PackagingWizardViewModel wizard, IServiceProvider serviceProvider)
        : base(wizard)
    {
        _metadataClient = serviceProvider.GetRequiredService<MetadataClient>();
        _logger = serviceProvider.GetRequiredService<ILogger<MetadataStepViewModel>>();
    }

    public override string Title => "Details";

    [ObservableProperty]
    private string _gameTitle = string.Empty;

    [ObservableProperty]
    private string _sortTitle = string.Empty;

    [ObservableProperty]
    private string _version = "1.0";

    [ObservableProperty]
    private DateTimeOffset? _releasedOn;

    [ObservableProperty]
    private bool _singleplayer;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    #region Metadata lookup

    [ObservableProperty]
    private bool _isSearchOpen;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string? _selectedProvider;

    [ObservableProperty]
    private MetadataSubProvider? _selectedSubProvider;

    [ObservableProperty]
    private string? _searchError;

    [ObservableProperty]
    private MetadataSearchResult? _selectedResult;

    public ObservableCollection<string> Providers { get; } = [];

    public ObservableCollection<MetadataSubProvider> SubProviders { get; } = [];

    public ObservableCollection<MetadataSearchResult> SearchResults { get; } = [];

    #endregion

    partial void OnGameTitleChanged(string value) =>
        CanGoNext = !string.IsNullOrWhiteSpace(value);

    public override Task OnEnterAsync()
    {
        if (string.IsNullOrWhiteSpace(GameTitle))
        {
            // The install folder's name is usually the game's name and is a better starting
            // point than an empty box.
            GameTitle = System.IO.Path.GetFileName(
                Package.InstallDirectory.TrimEnd(
                    System.IO.Path.DirectorySeparatorChar,
                    System.IO.Path.AltDirectorySeparatorChar)) ?? string.Empty;
        }

        CanGoNext = !string.IsNullOrWhiteSpace(GameTitle);

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task OpenSearchAsync()
    {
        IsSearchOpen = true;
        SearchError = null;

        if (string.IsNullOrWhiteSpace(SearchQuery))
            SearchQuery = GameTitle;

        if (Providers.Count > 0)
            return;

        try
        {
            foreach (var provider in await _metadataClient.GetProvidersAsync())
                Providers.Add(provider);

            SelectedProvider = Providers.FirstOrDefault();
        }
        catch (Exception ex)
        {
            SearchError = $"Could not load metadata providers: {ex.Message}";

            _logger.LogWarning(ex, "Could not load metadata providers");
        }
    }

    [RelayCommand]
    private void CloseSearch() => IsSearchOpen = false;

    partial void OnSelectedProviderChanged(string? value)
    {
        SubProviders.Clear();
        SelectedSubProvider = null;

        if (string.IsNullOrWhiteSpace(value))
            return;

        _ = LoadSubProvidersAsync(value);
    }

    private async Task LoadSubProvidersAsync(string provider)
    {
        try
        {
            foreach (var subProvider in await _metadataClient.GetSubProvidersAsync(provider))
                SubProviders.Add(subProvider);
        }
        catch (Exception ex)
        {
            // Not every provider has sub-providers; this is informational, not fatal.
            _logger.LogDebug(ex, "Could not load sub-providers for {Provider}", provider);
        }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedProvider) || string.IsNullOrWhiteSpace(SearchQuery))
            return;

        IsSearching = true;
        SearchError = null;

        SearchResults.Clear();

        try
        {
            var results = await _metadataClient.SearchAsync(
                SelectedProvider, SearchQuery, SelectedSubProvider?.Slug);

            foreach (var result in results.Results)
                SearchResults.Add(result);

            if (SearchResults.Count == 0)
                SearchError = "No matches were found.";
        }
        catch (Exception ex)
        {
            SearchError = $"Search failed: {ex.Message}";

            _logger.LogWarning(ex, "Metadata search failed");
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task ApplySelectedResultAsync()
    {
        var result = SelectedResult;

        if (result == null || string.IsNullOrWhiteSpace(SelectedProvider))
            return;

        IsSearching = true;

        try
        {
            // The search result carries a summary; fetch the full record before applying it.
            var game = await _metadataClient.GetGameAsync(SelectedProvider, result.Id);

            Apply(game ?? result.Data);

            IsSearchOpen = false;
        }
        catch (Exception ex)
        {
            SearchError = $"Could not load that entry: {ex.Message}";

            _logger.LogWarning(ex, "Could not load metadata for {Id}", result.Id);
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// Copies looked-up metadata onto the package, keeping the richer collections that only the
    /// provider can supply.
    /// </summary>
    private void Apply(SDK.Models.Manifest.Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.Title))
            GameTitle = game.Title;

        if (!string.IsNullOrWhiteSpace(game.SortTitle))
            SortTitle = game.SortTitle;

        if (!string.IsNullOrWhiteSpace(game.Description))
            Description = game.Description;

        if (!string.IsNullOrWhiteSpace(game.Notes))
            Notes = game.Notes;

        if (game.ReleasedOn != default)
            ReleasedOn = new DateTimeOffset(game.ReleasedOn);

        Singleplayer = game.Singleplayer;

        var manifest = Package.Manifest;

        manifest.Genres = game.Genres;
        manifest.Tags = game.Tags;
        manifest.Developers = game.Developers;
        manifest.Publishers = game.Publishers;
        manifest.Platforms = game.Platforms;
        manifest.MultiplayerModes = game.MultiplayerModes;
        manifest.Collections = game.Collections;
        manifest.ExternalIds = game.ExternalIds;
        manifest.Engine = game.Engine;
        manifest.Type = game.Type;
    }

    public override Task OnLeaveAsync()
    {
        var manifest = Package.Manifest;

        manifest.Title = GameTitle;
        manifest.SortTitle = string.IsNullOrWhiteSpace(SortTitle) ? GameTitle : SortTitle;
        manifest.Version = Version;
        manifest.ReleasedOn = ReleasedOn?.DateTime ?? default;
        manifest.Singleplayer = Singleplayer;
        manifest.Description = Description;
        manifest.Notes = Notes;

        return Task.CompletedTask;
    }
}
