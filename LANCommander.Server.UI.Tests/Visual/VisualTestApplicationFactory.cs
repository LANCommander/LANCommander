using LANCommander.Server.UI.Fixtures;
using LANCommander.Server.UI.Fixtures.Data;
using LANCommander.Server.UI.Providers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LANCommander.Server.UI.Tests.Visual;

/// <summary>
/// The UI test server, plus the fixture gallery routes and a clock pinned to
/// <see cref="FixtureIds.Epoch"/> so relative times ("3 days ago") render identically every run.
/// </summary>
public class VisualTestApplicationFactory : UITestApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(new UIRouteAssembly(FixturesAssembly.Assembly));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider, FixedLocalTimeProvider>();
        });
    }
}

/// <summary>A <see cref="LocalTimeProvider"/> whose "now" is always <see cref="FixtureIds.Epoch"/>.</summary>
public sealed class FixedLocalTimeProvider : LocalTimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(FixtureIds.Epoch);
}
