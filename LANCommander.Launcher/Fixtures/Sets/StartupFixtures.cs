#if DEBUG
using System;
using System.Collections.Generic;
using System.Net;
using LANCommander.Launcher.Models;
using LANCommander.Launcher.ViewModels;
using LANCommander.SDK.Models;

namespace LANCommander.Launcher.Fixtures.Sets;

/// <summary>What the launcher shows before the shell: splash, server selection and login.</summary>
public static class StartupFixtures
{
    public static IReadOnlyList<ViewFixture> All { get; } =
    [
        new("Startup.Splash", "Splash screen while connecting", context =>
        {
            var splash = context.Main.SplashViewModel;
            splash.UpdateStatus("Connecting to server...");

            return context.MainWindow(splash);
        }),

        ServerSelection("Startup.ServerSelection", "Choosing a server, none found on the network", servers =>
            servers.ServerAddress = string.Empty),

        ServerSelection("Startup.ServerSelection.Discovered", "Choosing a server from those found on the network", servers =>
        {
            servers.ServerAddress = string.Empty;
            servers.DiscoveredServers.Add(Server("Basement LAN", "http://192.168.1.20:1337", "2.2.0"));
            servers.DiscoveredServers.Add(Server("LAN Party 2026", "http://192.168.1.50:1337", "2.2.0"));
            servers.DiscoveredServers.Add(Server("LANCommander", "http://10.0.0.8:1337", "2.1.4"));
        }),

        ServerSelection("Startup.ServerSelection.Error", "A server address that couldn't be reached", servers =>
        {
            servers.ServerAddress = "http://192.168.1.99:1337";
            servers.HasError = true;
            servers.StatusMessage = "Connection failed: No LANCommander server responded at 192.168.1.99:1337.";
        }),

        Login("Startup.Login", "Signing in", _ => { }),

        Login("Startup.Login.Filled", "Signing in, with credentials entered", login =>
        {
            login.Username = "pat";
            login.Password = "hunter22";
        }),

        Login("Startup.Login.Error", "A failed sign-in", login =>
        {
            login.Username = "pat";
            login.HasError = true;
            login.StatusMessage = "Login failed: Invalid username or password.";
        }),

        Login("Startup.Login.Register", "Creating an account", login =>
        {
            login.IsRegistering = true;
            login.Username = "newplayer";
            login.PasswordRequirements = "At least 8 characters, with a number and a symbol.";
        }),

        Login("Startup.Login.Providers", "Signing in with a password or an external provider", login =>
        {
            login.AuthenticationProviders.Add(new AuthenticationProvider { Name = "Authentik", Slug = "authentik" });
            login.AuthenticationProviders.Add(new AuthenticationProvider { Name = "Discord", Slug = "discord" });
        }),

        Login("Startup.Login.ProvidersOnly", "A server that only allows external providers", login =>
        {
            login.AllowPassword = false;
            login.AuthenticationProviders.Add(new AuthenticationProvider { Name = "Authentik", Slug = "authentik" });
            login.AuthenticationProviders.Add(new AuthenticationProvider { Name = "Discord", Slug = "discord" });
        }),
    ];

    private static DiscoveredServer Server(string name, string address, string version)
    {
        var uri = new Uri(address);

        return new DiscoveredServer(
            new BeaconMessage { Name = name, Address = address, Version = version },
            new IPEndPoint(IPAddress.Parse(uri.Host), uri.Port));
    }

    private static ViewFixture ServerSelection(string name, string description, Action<ServerSelectionViewModel> configure) =>
        new(name, description, context =>
        {
            var servers = context.Main.ServerSelectionViewModel;

            configure(servers);

            return context.MainWindow(servers);
        });

    private static ViewFixture Login(string name, string description, Action<LoginViewModel> configure) =>
        new(name, description, context =>
        {
            var login = context.Main.LoginViewModel;

            configure(login);

            return context.MainWindow(login);
        });
}
#endif
