using Avalonia.Controls;
using Avalonia.Interactivity;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Clients;
using LANCommander.SDK.Services;

namespace LANCommander.Packager.Views;

public partial class ConnectDialog : Window
{
    private readonly AuthenticationClient _authClient;
    private readonly IConnectionClient _connectionClient;
    private readonly ISettingsProvider _settingsProvider;

    public bool IsAuthenticated { get; private set; }

    public ConnectDialog()
    {
        InitializeComponent();
    }

    public ConnectDialog(
        AuthenticationClient authClient,
        IConnectionClient connectionClient,
        ISettingsProvider settingsProvider)
    {
        _authClient = authClient;
        _connectionClient = connectionClient;
        _settingsProvider = settingsProvider;
        InitializeComponent();

        var currentAddress = _settingsProvider.CurrentValue.Authentication.ServerAddress;
        if (currentAddress != null)
            ServerAddressField.Text = currentAddress.ToString();

        CancelButton.Click += (_, _) => Close(false);
        ConnectButton.Click += OnConnectClick;
        DisconnectButton.Click += OnDisconnectClick;

        var token = _settingsProvider.CurrentValue.Authentication.Token;
        if (token != null && !string.IsNullOrEmpty(token.AccessToken))
        {
            DisconnectButton.IsVisible = true;
        }
    }

    private async void OnConnectClick(object? sender, RoutedEventArgs e)
    {
        ErrorLabel.IsVisible = false;
        ConnectButton.IsEnabled = false;
        ConnectButton.Content = "Connecting...";

        try
        {
            var address = ServerAddressField.Text?.Trim();
            var username = UsernameField.Text?.Trim();
            var password = PasswordField.Text;

            if (string.IsNullOrWhiteSpace(address))
            {
                ShowError("Please enter a server address.");
                return;
            }

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter username and password.");
                return;
            }

            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
                uri = new Uri($"http://{address}");

            await _connectionClient.UpdateServerAddressAsync(uri);
            await _authClient.AuthenticateAsync(username, password, uri);

            IsAuthenticated = true;
            Close(true);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            ConnectButton.IsEnabled = true;
            ConnectButton.Content = "Connect";
        }
    }

    private void OnDisconnectClick(object? sender, RoutedEventArgs e)
    {
        _settingsProvider.Update(s =>
        {
            s.Authentication.Token = null;
            s.Authentication.ServerAddress = null;
        });

        IsAuthenticated = false;
        DisconnectButton.IsVisible = false;
        Close(false);
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }
}
