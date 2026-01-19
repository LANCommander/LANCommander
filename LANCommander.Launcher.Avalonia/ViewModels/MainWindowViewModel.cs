using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConnectionClient _connectionClient;
    private readonly AuthenticationService _authenticationService;
    private readonly SettingsProvider<Settings.Settings> _settingsProvider;

    [ObservableProperty]
    private ViewModelBase _currentView;

    [ObservableProperty]
    private string _title = "LANCommander Launcher";

    public ServerSelectionViewModel ServerSelectionViewModel { get; }
    public LoginViewModel LoginViewModel { get; }
    public ShellViewModel ShellViewModel { get; }

    public MainWindowViewModel(
        IServiceProvider serviceProvider,
        IConnectionClient connectionClient,
        AuthenticationService authenticationService,
        SettingsProvider<Settings.Settings> settingsProvider)
    {
        _serviceProvider = serviceProvider;
        _connectionClient = connectionClient;
        _authenticationService = authenticationService;
        _settingsProvider = settingsProvider;

        ServerSelectionViewModel = new ServerSelectionViewModel(connectionClient, settingsProvider);
        LoginViewModel = new LoginViewModel(connectionClient, authenticationService, settingsProvider);
        ShellViewModel = new ShellViewModel(serviceProvider);

        // Wire up navigation events
        ServerSelectionViewModel.ServerConnected += OnServerConnected;
        LoginViewModel.LoginSucceeded += OnLoginSucceeded;
        LoginViewModel.ChangeServerRequested += OnChangeServerRequested;
        ShellViewModel.LogoutRequested += OnLogoutRequested;

        // Start with server selection
        _currentView = ServerSelectionViewModel;
    }

    public async Task InitializeAsync()
    {
        // Check if we have a saved server address and valid token
        var settings = _settingsProvider.CurrentValue;
        
        if (settings.Authentication?.ServerAddress != null)
        {
            await _connectionClient.UpdateServerAddressAsync(settings.Authentication.ServerAddress.ToString());

            if (_authenticationService.HasStoredCredentials())
            {
                try
                {
                    // Try to login with stored credentials
                    await _authenticationService.Login();
                    
                    if (_connectionClient.IsConnected())
                    {
                        // Token is valid - go directly to shell
                        CurrentView = ShellViewModel;
                        await ShellViewModel.InitializeAsync();
                        return;
                    }
                }
                catch
                {
                    // Token validation failed - continue to login
                }
            }

            // We have a server but no valid token - go to login
            LoginViewModel.ServerAddress = settings.Authentication.ServerAddress.ToString();
            CurrentView = LoginViewModel;
        }
        // Otherwise stay on server selection (default)
    }

    private void OnServerConnected(object? sender, EventArgs e)
    {
        LoginViewModel.ServerAddress = _connectionClient.GetServerAddress()?.ToString() ?? string.Empty;
        CurrentView = LoginViewModel;
    }

    private async void OnLoginSucceeded(object? sender, EventArgs e)
    {
        CurrentView = ShellViewModel;
        await ShellViewModel.InitializeAsync();
    }

    private void OnChangeServerRequested(object? sender, EventArgs e)
    {
        CurrentView = ServerSelectionViewModel;
    }

    private void OnLogoutRequested(object? sender, EventArgs e)
    {
        CurrentView = LoginViewModel;
    }
}
