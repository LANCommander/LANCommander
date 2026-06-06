using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Models;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.ViewModels;

public partial class ServerSelectionViewModel : ViewModelBase, IDisposable
{
    private readonly IConnectionClient _connectionClient;
    private readonly SettingsProvider<Settings.Settings> _settingsProvider;
    private readonly BeaconClient _beaconClient;
    private CancellationTokenSource? _discoveryToken;

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _isScanning;

    public ObservableCollection<DiscoveredServer> DiscoveredServers { get; } = new();

    public event EventHandler? ServerConnected;

    public ServerSelectionViewModel(
        IConnectionClient connectionClient,
        SettingsProvider<Settings.Settings> settingsProvider,
        BeaconClient beaconClient)
    {
        _connectionClient = connectionClient;
        _settingsProvider = settingsProvider;
        _beaconClient = beaconClient;

        _beaconClient.OnBeaconResponse += OnBeaconResponse;

        // Load saved server address if available
        if (_settingsProvider.CurrentValue.Authentication?.ServerAddress != null)
            ServerAddress = _settingsProvider.CurrentValue.Authentication.ServerAddress.ToString();

        _ = ScanAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (string.IsNullOrWhiteSpace(ServerAddress))
        {
            StatusMessage = "Please enter a server address";
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;
        StatusMessage = "Testing connection...";

        try
        {
            // UpdateServerAddressAsync discovers and validates the server internally
            // (pings candidate URIs). If it returns without throwing, the server is reachable.
            await _connectionClient.UpdateServerAddressAsync(ServerAddress);

            ServerAddress = _connectionClient.GetServerAddress().ToString();

            StatusMessage = "Connected!";
            ServerConnected?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsScanning)
            return;

        try
        {
            IsScanning = true;

            _discoveryToken?.Dispose();
            _discoveryToken = new CancellationTokenSource();

            await _beaconClient.StartProbeAsync(cancellationToken: _discoveryToken.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelled
        }
        finally
        {
            _beaconClient.CleanupProbe();
            IsScanning = false;
        }
    }

    [RelayCommand]
    private void CancelScan()
    {
        _discoveryToken?.Cancel();
    }

    [RelayCommand]
    private void SelectServer(DiscoveredServer server)
    {
        ServerAddress = server.Address.ToString();
    }

    private void OnBeaconResponse(object sender, BeaconResponseArgs e)
    {
        var discoveredServer = new DiscoveredServer(e.Message, e.EndPoint);

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (DiscoveredServers.All(s => s.Address != discoveredServer.Address))
                DiscoveredServers.Add(discoveredServer);
        });
    }

    public void Dispose()
    {
        _beaconClient.OnBeaconResponse -= OnBeaconResponse;
        _discoveryToken?.Cancel();
        _discoveryToken?.Dispose();
    }
}
