using System.Data.Common;
using LANCommander.Server.Data;
using Microsoft.Data.Sqlite;
using LANCommander.Server.Services.Abstractions;
using LANCommander.Server.Settings.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Octokit;
using Semver;

namespace LANCommander.Server.UI.Tests;

/// <summary>
/// WebApplicationFactory that starts a real Kestrel server for Playwright browser tests.
/// Uses the "dual host" pattern: builds the real app with Kestrel from the configured
/// builder, and returns a dummy TestServer host to satisfy WebApplicationFactory's internals.
/// In .NET 9, WebApplicationFactory hard-casts IServer to TestServer, so we need this workaround.
/// </summary>
public class UITestApplicationFactory : WebApplicationFactory<Program>
{
    private IHost? _realHost;
    private string? _dbPath;
    public string BaseAddress { get; private set; } = default!;
    public IServiceProvider RealServices => _realHost!.Services;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the content root to the server project directory so static files are found
        builder.UseContentRoot(FindServerProjectDirectory());

        // The login page uses relative paths for screenshot backgrounds.
        // Create the expected directory so it doesn't throw DirectoryNotFoundException.
        Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "wwwroot", "static", "login"));

        builder.ConfigureServices(services =>
        {
            // Replace database with in-memory (same pattern as existing ApplicationFactory)
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDbContextOptionsConfiguration<DatabaseContext>));
            if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

            var dbConnectionDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbConnection));
            if (dbConnectionDescriptor != null) services.Remove(dbConnectionDescriptor);

            // Use a file-based SQLite database rather than the EF InMemory provider.
            // The app's DataTable queries use relational features (AsSplitQuery, Include,
            // and a translated punctuation-stripping search expression) that the InMemory
            // provider cannot translate — on CI this surfaced as a native stack overflow in
            // CountAsync that crashed the in-process server and cascaded into timeouts.
            // A real SQLite file supports those queries and concurrent connections.
            _dbPath = Path.Combine(Path.GetTempPath(), $"LANCommander_UITest_{Guid.NewGuid():N}.db");

            services.AddDbContextFactory<DatabaseContext>(optionsBuilder =>
            {
                optionsBuilder.UseSqlite(
                    $"Data Source={_dbPath}",
                    options => options.MigrationsAssembly("LANCommander.Server.Data.SQLite"));
            });

            // Mock IVersionProvider
            var versionProviderDescriptor = services.SingleOrDefault(
                d => typeof(IVersionProvider).IsAssignableFrom(d.ServiceType));
            if (versionProviderDescriptor != null) services.Remove(versionProviderDescriptor);
            services.AddSingleton<IVersionProvider, StubVersionProvider>();

            // Mock IGitHubService
            var gitHubServiceDescriptor = services.SingleOrDefault(
                d => typeof(IGitHubService).IsAssignableFrom(d.ServiceType));
            if (gitHubServiceDescriptor != null) services.Remove(gitHubServiceDescriptor);
            services.AddSingleton<IGitHubService, StubGitHubService>();

            // Remove Hangfire hosted services to prevent stack overflow during process shutdown.
            // The Hangfire background job server has a deep disposal chain that can overflow the stack.
            var hangfireHostedServices = services.Where(
                d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                    && d.ImplementationType?.FullName?.Contains("Hangfire") == true).ToList();
            foreach (var svc in hangfireHostedServices) services.Remove(svc);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Build the REAL host with Kestrel (the builder has all configured services from Program.cs).
        // Use explicit ListenLocalhost(0) to override the URL-based configuration from Program.cs.
        builder.ConfigureWebHost(wb =>
        {
            wb.UseKestrel(options =>
            {
                options.Listen(System.Net.IPAddress.Loopback, 0);
            });
        });
        _realHost = builder.Build();
        _realHost.Start();

        // Create the SQLite schema from the current model before any requests run.
        using (var scope = _realHost.Services.CreateScope())
        {
            var contextFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<DatabaseContext>>();
            using var context = contextFactory.CreateDbContext();
            context.Database.EnsureCreated();
        }

        // Get the dynamically assigned port
        var server = _realHost.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>();
        BaseAddress = addresses!.Addresses.First();

        // Create a DUMMY host with TestServer to satisfy WebApplicationFactory's internal cast.
        // WebApplicationFactory in .NET 9 hard-casts IServer to TestServer after CreateHost returns.
        var dummyBuilder = new HostBuilder();
        dummyBuilder.ConfigureWebHost(wb =>
        {
            wb.UseTestServer();
            wb.Configure(app => { });
        });
        var dummyHost = dummyBuilder.Build();
        dummyHost.Start();

        return dummyHost;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_realHost != null)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _realHost.StopAsync(cts.Token);
            }
            catch
            {
                // Suppress shutdown errors
            }

            // Do NOT call _realHost.Dispose() — the DI container's deep dependency
            // chain (Hangfire, EF, SignalR, etc.) causes a native stack overflow that
            // cannot be caught. Stopping the host is sufficient for test cleanup.
        }

        try { await base.DisposeAsync(); } catch { }

        // Release pooled SQLite connections so the temp database file can be deleted.
        if (_dbPath != null)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(_dbPath))
                    File.Delete(_dbPath);
            }
            catch
            {
                // Best-effort cleanup of the temp database file.
            }
        }

        GC.SuppressFinalize(this);
    }

    private static string FindServerProjectDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "LANCommander.Server");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "LANCommander.Server.csproj")))
                return candidate;

            var slnx = Path.Combine(dir, "LANCommander.slnx");
            if (File.Exists(slnx))
            {
                candidate = Path.Combine(dir, "LANCommander.Server");
                if (Directory.Exists(candidate))
                    return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new DirectoryNotFoundException("Could not find LANCommander.Server project directory");
    }
}

/// <summary>
/// Simple stub for IVersionProvider in UI tests.
/// </summary>
internal class StubVersionProvider : IVersionProvider
{
    public SemVersion GetCurrentVersion() => SemVersion.Parse("1.0.0");
    public ReleaseChannel GetReleaseChannel(SemVersion version) => ReleaseChannel.Stable;
}

/// <summary>
/// Simple stub for IGitHubService in UI tests.
/// </summary>
internal class StubGitHubService : IGitHubService
{
    public Task<SemVersion> GetLatestVersionAsync(ReleaseChannel releaseChannel)
        => Task.FromResult(SemVersion.Parse("1.0.0"));

    public Task<Release?> GetReleaseAsync(SemVersion version)
        => Task.FromResult<Release?>(null);

    public Task<Release?> GetReleaseAsync(string tag)
        => Task.FromResult<Release?>(null);

    public Task<IEnumerable<Release>> GetReleasesAsync(int count)
        => Task.FromResult<IEnumerable<Release>>(Array.Empty<Release>());

    public Task<IEnumerable<Artifact>> GetNightlyArtifactsAsync(string versionOverride = null)
        => Task.FromResult<IEnumerable<Artifact>>(Array.Empty<Artifact>());

    public Task<IEnumerable<Artifact>> GetWorkflowArtifactsAsync(long runId)
        => Task.FromResult<IEnumerable<Artifact>>(Array.Empty<Artifact>());
}
