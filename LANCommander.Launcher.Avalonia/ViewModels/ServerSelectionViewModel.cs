using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class ServerSelectionViewModel : ViewModelBase
{
    private readonly IConnectionClient _connectionClient;
    private readonly SettingsProvider<Settings.Settings> _settingsProvider;

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    public event EventHandler? ServerConnected;

    public ServerSelectionViewModel(
        IConnectionClient connectionClient, 
        SettingsProvider<Settings.Settings> settingsProvider)
    {
        _connectionClient = connectionClient;
        _settingsProvider = settingsProvider;

        // Load saved server address if available
        if (_settingsProvider.CurrentValue.Authentication?.ServerAddress != null)
            ServerAddress = _settingsProvider.CurrentValue.Authentication.ServerAddress.ToString();
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
            await _connectionClient.UpdateServerAddressAsync(ServerAddress);
            var canConnect = await _connectionClient.PingAsync();
            
            if (canConnect)
            {
                _settingsProvider.Update(s =>
                {
                    s.Authentication.ServerAddress = new Uri(ServerAddress);
                });
                
                StatusMessage = "Connected!";
                ServerConnected?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusMessage = "Could not connect to server. Please check the address and try again.";
                HasError = true;
            }
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
}
