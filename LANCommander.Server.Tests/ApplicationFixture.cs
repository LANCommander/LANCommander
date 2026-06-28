using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using LANCommander.SDK.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.Server.Tests;

public class ApplicationFixture : ApplicationFactory<Program>
{
    public static ApplicationFixture Instance;

    /// <summary>Server-side service provider (the in-memory app under test).</summary>
    public IServiceProvider ServiceProvider { get; set; }

    /// <summary>Client-side SDK service provider, wired to talk to the in-memory server.</summary>
    public IServiceProvider ClientServiceProvider { get; set; }

    /// <summary>HttpClient whose handler routes to the in-memory server.</summary>
    public HttpClient HttpClient { get; }

    public Uri ServerAddress { get; }

    public AuthenticationClient AuthenticationClient { get; }
    public GameClient GameClient { get; }
    public SaveClient SaveClient { get; }
    public TagClient TagClient { get; }

    public ApplicationFixture(ApplicationFactory<Program> factory)
    {
        if (Instance != null)
            return;

        ServiceProvider = factory.Services;

        HttpClient = factory.CreateClient();
        ServerAddress = HttpClient.BaseAddress!;

        // Build a separate SDK client container (mirrors the launcher's composition) whose
        // injected HttpClient is the in-memory test handler, so all API calls route to the
        // server under test instead of hitting the network.
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddOptions<SDK.Models.Settings>().Configure(_ => { });
        services.AddSingleton<IServerConfigurationRefresher>(NoopRefresher.Instance);
        services.AddLANCommanderClient<SDK.Models.Settings>();
        services.AddSingleton(HttpClient);

        ClientServiceProvider = services.BuildServiceProvider();

        ClientServiceProvider.GetRequiredService<IServerAddressProvider>().SetServerAddress(ServerAddress);

        AuthenticationClient = ClientServiceProvider.GetRequiredService<AuthenticationClient>();
        GameClient = ClientServiceProvider.GetRequiredService<GameClient>();
        SaveClient = ClientServiceProvider.GetRequiredService<SaveClient>();
        TagClient = ClientServiceProvider.GetRequiredService<TagClient>();

        Instance = this;
    }

    /// <summary>Authenticates against the in-memory server and stores the token for subsequent client calls.</summary>
    public Task AuthenticateAsync(string username, string password)
        => AuthenticationClient.AuthenticateAsync(username, password, ServerAddress);

    private sealed class NoopRefresher : IServerConfigurationRefresher
    {
        public static readonly NoopRefresher Instance = new();
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

[CollectionDefinition("Application")]
public class ApplicationCollection : ICollectionFixture<ApplicationFactory<Program>>
{

}
