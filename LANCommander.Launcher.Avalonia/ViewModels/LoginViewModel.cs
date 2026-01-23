using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IConnectionClient _connectionClient;
    private readonly AuthenticationService _authenticationService;
    private readonly SettingsProvider<Settings.Settings> _settingsProvider;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _serverAddress = string.Empty;

    [ObservableProperty]
    private bool _isServerOffline;

    public event EventHandler? LoginSucceeded;
    public event EventHandler? ChangeServerRequested;

    public LoginViewModel(
        IConnectionClient connectionClient,
        AuthenticationService authenticationService,
        SettingsProvider<Settings.Settings> settingsProvider)
    {
        _connectionClient = connectionClient;
        _authenticationService = authenticationService;
        _settingsProvider = settingsProvider;

        ServerAddress = _connectionClient.GetServerAddress()?.ToString() ?? "Not connected";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (IsServerOffline)
        {
            StatusMessage = "Server is offline. Please try again later or change server.";
            HasError = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "Please enter username and password";
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;
        StatusMessage = "Logging in...";

        try
        {
            var serverAddress = _connectionClient.GetServerAddress();
            if (serverAddress == null)
            {
                StatusMessage = "No server configured";
                HasError = true;
                return;
            }

            await _authenticationService.Login(serverAddress, Username, Password);

            StatusMessage = "Login successful!";
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Login failed: {ex.Message}";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ChangeServer()
    {
        ChangeServerRequested?.Invoke(this, EventArgs.Empty);
    }
}
