#if DEBUG
using LANCommander.Server.UI;
using LANCommander.Server.UI.Fixtures;
using LANCommander.Server.UI.Fixtures.Data;

namespace LANCommander.Server.Startup;

/// <summary>
/// Debug-only access to the UI fixtures: the control gallery at <c>/_fixtures</c>, and an opt-in
/// seeded database for working on pages that need data.
/// </summary>
public static class UIFixtures
{
    public const string SeedVariable = "LANCOMMANDER_FIXTURE_SEED";

    public static WebApplicationBuilder AddUIFixtures(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(new UIRouteAssembly(FixturesAssembly.Assembly));

        return builder;
    }

    /// <summary>
    /// Seeds the fixture data set when <see cref="SeedVariable"/> is <c>1</c> and the database has
    /// no games yet. Never touches a database that already has content.
    /// </summary>
    public static async Task SeedUIFixturesIfRequestedAsync(this WebApplication app)
    {
        if (Environment.GetEnvironmentVariable(SeedVariable) is not ("1" or "true"))
            return;

        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        var seeder = new FixtureDatabaseSeeder(app.Services);

        if (await seeder.IsSeededAsync())
        {
            logger.LogInformation("{Variable} is set but the database already has games; skipping fixture seeding", SeedVariable);

            return;
        }

        logger.LogInformation("Seeding UI fixture data");

        await seeder.SeedAsync(Path.Combine(AppContext.BaseDirectory, "FixtureStorage"));
    }
}
#endif
