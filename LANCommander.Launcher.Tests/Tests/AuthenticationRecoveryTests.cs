using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.ViewModels;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Exceptions;
using LANCommander.SDK.Factories;
using LANCommander.SDK.Models;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LANCommander.Launcher.Tests.Tests;

public class AuthenticationRecoveryTests
{
    [AvaloniaFact]
    public async Task Startup_does_not_enter_the_shell_when_the_rpc_connection_has_an_expired_session()
    {
        using var fixture = new RecoveryFixture();
        Assert.True(fixture.Connection.IsConnected());

        await fixture.ViewModel.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(fixture.ViewModel.LoginViewModel, fixture.ViewModel.CurrentView);
        Assert.False(fixture.ViewModel.IsShellActive);
        Assert.True(fixture.ViewModel.LoginViewModel.HasError);
        Assert.Contains("session has expired", fixture.ViewModel.LoginViewModel.StatusMessage);
    }

    [AvaloniaFact]
    public async Task Expiry_returns_to_sign_in_and_keeps_the_external_provider_button_available()
    {
        using var fixture = new RecoveryFixture();
        fixture.ViewModel.CurrentView = fixture.ViewModel.ShellViewModel;
        fixture.ViewModel.LoginViewModel.Password = "old-password";
        fixture.ViewModel.LoginViewModel.AutoRedirectToProvider = true;

        await Assert.ThrowsAsync<AuthFailedException>(() =>
            fixture.Requests.Create().UseAuthenticationToken().UseRoute("/api/Depot").GetAsync<string[]>());
        Dispatcher.UIThread.RunJobs();

        var login = fixture.ViewModel.LoginViewModel;
        Assert.Same(login, fixture.ViewModel.CurrentView);
        Assert.False(fixture.ViewModel.IsShellActive);
        Assert.True(login.HasError);
        Assert.Equal("Your session has expired. Please sign in again.", login.StatusMessage);
        Assert.Equal(string.Empty, login.Password);
        Assert.True(login.ShowProviderButtons);
        Assert.False(login.AutoRedirectToProvider);
        Assert.True(fixture.Connection.Disconnected);
    }

    private sealed class RecoveryFixture : IDisposable
    {
        private readonly HttpClient _httpClient = new(new RecoveryHandler());
        private readonly ServiceProvider _services;
        private readonly AuthenticationSession _session;
        public ConnectedClient Connection { get; } = new();
        public MainWindowViewModel ViewModel { get; }
        public ApiRequestFactory Requests { get; }

        public RecoveryFixture()
        {
            var settings = new Launcher.Settings.Settings();
            settings.Authentication.ServerAddress = Connection.GetServerAddress();
            settings.Authentication.Token = new AuthToken
            {
                AccessToken = new JwtSecurityTokenHandler().WriteToken(
                    new JwtSecurityToken(expires: DateTime.UtcNow.AddMinutes(-10))),
                RefreshToken = "expired-refresh-token",
                Expiration = DateTime.UtcNow.AddMinutes(-10),
            };
            var inMemorySettings = new InMemorySettings(settings);
            var tokens = new TokenProvider(inMemorySettings);
            _session = new AuthenticationSession(
                tokens, inMemorySettings, NullLogger<AuthenticationSession>.Instance);
            Requests = new ApiRequestFactory(_httpClient, tokens, inMemorySettings, _session);
            var authenticationClient = new AuthenticationClient(
                NullLogger<AuthenticationClient>.Instance, tokens, null!, inMemorySettings,
                Requests, Connection, null!);
            var authentication = new AuthenticationService(
                tokens, inMemorySettings, null!, Connection, authenticationClient,
                NullLogger<AuthenticationService>.Instance);
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<Launcher.Settings.Settings>().Configure(options =>
            {
                options.Authentication = settings.Authentication;
            });
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton(authenticationClient);
            services.AddSingleton(_session);
            services.AddSingleton(new BeaconClient(
                NullLogger<BeaconClient>.Instance, new NoNetworkInterfaces()));
            _services = services.BuildServiceProvider();
            var settingsProvider = new SettingsProvider<Launcher.Settings.Settings>(
                _services.GetRequiredService<IOptionsMonitor<Launcher.Settings.Settings>>());
            ViewModel = new MainWindowViewModel(_services, Connection, authentication, settingsProvider);
        }

        public void Dispose()
        {
            ViewModel.ServerSelectionViewModel.Dispose();
            _services.Dispose();
            _session.Dispose();
            _httpClient.Dispose();
        }
    }

    private sealed class InMemorySettings(Launcher.Settings.Settings settings) : ISettingsProvider
    {
        public SDK.Models.Settings CurrentValue => settings;
        public void Update(Action<SDK.Models.Settings> patch) => patch(settings);
    }

    private sealed class RecoveryHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/Auth/Refresh")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));

            var body = request.RequestUri.AbsolutePath == "/api/Auth/AuthenticationProviders"
                ? JsonSerializer.Serialize(new[] { new { Name = "External sign-in", Slug = "external" } })
                : JsonSerializer.Serialize(new
                {
                    Authentication = new
                    {
                        AutoRedirectToProvider = true,
                        AllowPassword = false,
                        AllowRegistration = false,
                    },
                });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body),
            });
        }
    }

    private sealed class NoNetworkInterfaces : INetworkInformationProvider
    {
        public string GetMacAddress() => string.Empty;
        public string GetComputerName() => string.Empty;
        public string GetIpAddress() => string.Empty;
        public IEnumerable<NetworkInterface> GetNetworkInterfaces() => [];
        public IEnumerable<IPAddress> GetBroadcastAddresses() => [];
    }

    private sealed class ConnectedClient : IConnectionClient
    {
        public event EventHandler? OnConnect;
        public event EventHandler? OnDisconnect;
        public event EventHandler? OnServerAddressChanged;
        public event EventHandler? OnOfflineModeEnabled;
        public bool Disconnected { get; private set; }
        public bool IsConnected() => !Disconnected;
        public bool IsConfigured() => true;
        public bool IsOfflineMode() => false;
        public bool HasServerAddress() => true;
        public Uri GetServerAddress() => new("https://server.test");
        public Task UpdateServerAddressAsync(string address) => Task.CompletedTask;
        public Task UpdateServerAddressAsync(Uri address) => Task.CompletedTask;
        public Task<bool> ConnectAsync() => Task.FromResult(true);
        public Task<bool> DisconnectAsync()
        {
            Disconnected = true;
            return Task.FromResult(true);
        }
        public Task EnableOfflineModeAsync() => Task.CompletedTask;
        public Task<bool> PingAsync(Uri? serverAddress = null) => Task.FromResult(true);
    }
}
