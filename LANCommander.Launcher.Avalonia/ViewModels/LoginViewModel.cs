using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LANCommander.Launcher.Services;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using AuthenticationProvider = LANCommander.SDK.Models.AuthenticationProvider;

namespace LANCommander.Launcher.Avalonia.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly IConnectionClient _connectionClient;
    private readonly AuthenticationService _authenticationService;
    private readonly AuthenticationClient _authenticationClient;
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

    public ObservableCollection<AuthenticationProvider> AuthenticationProviders { get; } = new();

    public event EventHandler? LoginSucceeded;
    public event EventHandler? ChangeServerRequested;

    public LoginViewModel(
        IConnectionClient connectionClient,
        AuthenticationService authenticationService,
        AuthenticationClient authenticationClient,
        SettingsProvider<Settings.Settings> settingsProvider)
    {
        _connectionClient = connectionClient;
        _authenticationService = authenticationService;
        _authenticationClient = authenticationClient;
        _settingsProvider = settingsProvider;

        ServerAddress = _connectionClient.GetServerAddress()?.ToString() ?? "Not connected";
    }

    public async Task LoadAuthenticationProvidersAsync()
    {
        AuthenticationProviders.Clear();

        try
        {
            var providers = await _authenticationClient.GetAuthenticationProvidersAsync();

            if (providers != null)
            {
                foreach (var provider in providers)
                    AuthenticationProviders.Add(provider);
            }
        }
        catch
        {
            // External providers unavailable - username/password login still works
        }
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
    private async Task LoginWithProviderAsync(AuthenticationProvider provider)
    {
        if (IsServerOffline)
        {
            StatusMessage = "Server is offline. Please try again later or change server.";
            HasError = true;
            return;
        }

        IsLoading = true;
        HasError = false;
        StatusMessage = $"Signing in with {provider.Name}...";

        try
        {
            var serverAddress = _connectionClient.GetServerAddress();
            if (serverAddress == null)
            {
                StatusMessage = "No server configured";
                HasError = true;
                return;
            }

            var requestId = Guid.NewGuid().ToString();
            var loginUrl = _authenticationClient.GetAuthenticationProviderLoginUrl(provider.Slug, requestId);

            OpenBrowser(loginUrl);

            var token = await PollForTokenAsync(requestId, TimeSpan.FromMinutes(5));

            if (token != null)
            {
                await _authenticationService.Login(serverAddress, token);
                StatusMessage = "Login successful!";
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                StatusMessage = "Login timed out or was cancelled.";
                HasError = true;
            }
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

    private CancellationTokenSource? _pollCts;

    private async Task<LANCommander.SDK.Models.AuthToken?> PollForTokenAsync(string requestId, TimeSpan timeout)
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource(timeout);

        try
        {
            while (!_pollCts.Token.IsCancellationRequested)
            {
                var token = await _authenticationClient.RedeemTokenAsync(requestId);

                if (token != null)
                    return token;

                await Task.Delay(2000, _pollCts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout or cancellation
        }

        return null;
    }

    private static void OpenBrowser(Uri url)
    {
        var urlString = url.ToString();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(urlString) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", urlString);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", urlString);
    }

    [RelayCommand]
    private void ChangeServer()
    {
        _pollCts?.Cancel();
        ChangeServerRequested?.Invoke(this, EventArgs.Empty);
    }
}
