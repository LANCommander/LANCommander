using LANCommander.Launcher.Data;
using LANCommander.Launcher.Services;
using LANCommander.Launcher.Services.Extensions;
using LANCommander.SDK.Extensions;
using LANCommander.SDK.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LANCommander.Launcher.Avalonia.IntegrationTests.Tests;

/// <summary>
/// Catches DI registration drift in the Avalonia launcher's service graph. If someone
/// removes/renames a registration that <c>AddLANCommanderLauncher()</c> promises, these
/// resolves throw and the test fails — well before a runtime crash would surface it.
///
/// Theory rather than a single Fact: each row builds its own ServiceProvider so a single
/// service that hangs/fails on resolve doesn't take the whole graph down with it. Tests
/// are also lighter to schedule across cores.
/// </summary>
public class LauncherDiCompositionTests
{
    [Theory]
    // FilterService is intentionally omitted — defined in LANCommander.Launcher.Services
    // but not registered by AddLANCommanderLauncher() and not consumed by the Avalonia
    // launcher today. Add it here once a future change wires it up.
    [InlineData(typeof(GameService))]
    [InlineData(typeof(LibraryService))]
    [InlineData(typeof(InstallService))]
    [InlineData(typeof(ImportService))]
    [InlineData(typeof(AuthenticationService))]
    [InlineData(typeof(CommandLineService))]
    [InlineData(typeof(ProfileService))]
    [InlineData(typeof(PlaySessionService))]
    [InlineData(typeof(SaveService))]
    public void Service_resolves_from_launcher_DI_graph(Type serviceType)
    {
        var services = BuildLauncherServices();

        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        var instance = scope.ServiceProvider.GetService(serviceType);

        instance.ShouldNotBeNull(
            customMessage: $"{serviceType.Name} must resolve from the launcher DI graph. " +
                           "If this fails, AddLANCommanderLauncher() likely lost a registration.");
    }

    /// <summary>
    /// Replicates the Avalonia launcher's <c>App.axaml.cs.ConfigureServices</c> with two swaps:
    /// EF InMemory in place of file-backed SQLite, and a no-op <see cref="IServerConfigurationRefresher"/>
    /// so no HTTP calls fire during construction.
    /// </summary>
    private static IServiceCollection BuildLauncherServices()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddHttpClient();
        services.AddOptions<Settings.Settings>().Configure(_ => { });
        services.AddSingleton<IServerConfigurationRefresher>(NoopRefresher.Instance);

        services.AddLANCommanderClient<Settings.Settings>();
        services.AddLANCommanderLauncher();

        // The launcher registers DatabaseContext using only EnableSensitiveDataLogging, so
        // OnConfiguring would otherwise call UseSqlite via AppPaths. Replace with EF InMemory.
        var dbDescriptors = services
            .Where(s => s.ServiceType == typeof(DbContextOptions<DatabaseContext>) ||
                        s.ServiceType == typeof(DbContextOptions))
            .ToList();
        foreach (var d in dbDescriptors) services.Remove(d);
        services.AddDbContext<DatabaseContext>(o => o.UseInMemoryDatabase($"di-{Guid.NewGuid()}"));

        return services;
    }

    private sealed class NoopRefresher : IServerConfigurationRefresher
    {
        public static readonly NoopRefresher Instance = new();
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
