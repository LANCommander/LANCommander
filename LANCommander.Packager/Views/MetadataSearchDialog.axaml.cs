using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;
using Game = LANCommander.SDK.Models.Manifest.Game;
using Key = Avalonia.Input.Key;

namespace LANCommander.Packager.Views;

public partial class MetadataSearchDialog : Window
{
    private readonly MetadataClient _metadataClient;
    private readonly ObservableCollection<Game> _results = new();
    private readonly List<MetadataSearchResult> _rawResults = new();
    private string? _selectedProvider;
    private int _offset;
    private bool _hasMore;

    public Game? SelectedGame { get; private set; }

    public MetadataSearchDialog()
    {
        InitializeComponent();
    }

    public MetadataSearchDialog(MetadataClient metadataClient, string? defaultSearch = null)
    {
        _metadataClient = metadataClient;
        InitializeComponent();

        ResultsList.ItemsSource = _results;
        ResultsList.SelectionChanged += (_, _) =>
            SelectButton.IsEnabled = ResultsList.SelectedItem != null;

        SearchButton.Click += async (_, _) => await SearchAsync();
        SelectButton.Click += OnSelectClick;
        CancelDialogButton.Click += (_, _) => Close(null);
        MoreButton.Click += async (_, _) => await LoadMoreAsync();

        ProviderCombo.SelectionChanged += async (_, _) =>
        {
            _selectedProvider = ProviderCombo.SelectedItem as string;
            if (_selectedProvider != null)
                await LoadSubProvidersAsync(_selectedProvider);
        };

        if (!string.IsNullOrWhiteSpace(defaultSearch))
            SearchField.Text = defaultSearch;

        _ = LoadProvidersAsync();
    }

    private async Task LoadProvidersAsync()
    {
        try
        {
            var providers = await _metadataClient.GetProvidersAsync();
            var list = providers.ToList();

            ProviderCombo.ItemsSource = list;

            if (list.Count > 0)
                ProviderCombo.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            EmptyLabel.Text = $"Failed to load providers: {ex.Message}";
        }
    }

    private async Task LoadSubProvidersAsync(string provider)
    {
        try
        {
            var subProviders = await _metadataClient.GetSubProvidersAsync(provider);
            var list = subProviders?.ToList();

            if (list != null && list.Count > 0)
            {
                SubProviderCombo.ItemsSource = list.Select(sp => sp.Name).ToList();
                SubProviderCombo.IsVisible = true;
                SubProviderCombo.SelectedIndex = 0;
            }
            else
            {
                SubProviderCombo.IsVisible = false;
                SubProviderCombo.SelectedItem = null;
            }
        }
        catch
        {
            SubProviderCombo.IsVisible = false;
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _ = SearchAsync();
    }

    private async Task SearchAsync()
    {
        var query = SearchField.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query) || _selectedProvider == null)
            return;

        _offset = 0;
        _results.Clear();
        _rawResults.Clear();

        LoadingPanel.IsVisible = true;
        EmptyLabel.IsVisible = false;
        SearchButton.IsEnabled = false;

        try
        {
            var subProvider = SubProviderCombo.IsVisible
                ? SubProviderCombo.SelectedItem as string
                : null;

            var results = await _metadataClient.SearchAsync(_selectedProvider, query, subProvider, 10, 0);

            if (results?.Results != null)
            {
                foreach (var result in results.Results)
                {
                    _rawResults.Add(result);
                    _results.Add(result.Data);
                }

                _hasMore = results.More;
                _offset = results.Offset + results.Limit;
                MoreButton.IsVisible = _hasMore;
            }

            if (_results.Count == 0)
                EmptyLabel.Text = "No results found.";

            EmptyLabel.IsVisible = _results.Count == 0;
        }
        catch (Exception ex)
        {
            EmptyLabel.Text = $"Search failed: {ex.Message}";
            EmptyLabel.IsVisible = true;
        }
        finally
        {
            LoadingPanel.IsVisible = false;
            SearchButton.IsEnabled = true;
        }
    }

    private async Task LoadMoreAsync()
    {
        if (_selectedProvider == null)
            return;

        var query = SearchField.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
            return;

        MoreButton.IsEnabled = false;

        try
        {
            var subProvider = SubProviderCombo.IsVisible
                ? SubProviderCombo.SelectedItem as string
                : null;

            var results = await _metadataClient.SearchAsync(_selectedProvider, query, subProvider, 10, _offset);

            if (results?.Results != null)
            {
                foreach (var result in results.Results)
                {
                    _rawResults.Add(result);
                    _results.Add(result.Data);
                }

                _hasMore = results.More;
                _offset = results.Offset + results.Limit;
            }

            MoreButton.IsVisible = _hasMore;
        }
        catch
        {
            MoreButton.IsVisible = false;
        }
        finally
        {
            MoreButton.IsEnabled = true;
        }
    }

    private async void OnSelectClick(object? sender, RoutedEventArgs e)
    {
        var selectedIndex = ResultsList.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex >= _rawResults.Count || _selectedProvider == null)
            return;

        SelectButton.IsEnabled = false;
        SelectButton.Content = "Loading...";

        try
        {
            var rawResult = _rawResults[selectedIndex];
            var fullGame = await _metadataClient.GetGameAsync(_selectedProvider, rawResult.Id);

            SelectedGame = fullGame;
            Close(fullGame);
        }
        catch (Exception ex)
        {
            EmptyLabel.Text = $"Failed to load game details: {ex.Message}";
            EmptyLabel.IsVisible = true;
            SelectButton.IsEnabled = true;
            SelectButton.Content = "Select";
        }
    }
}
