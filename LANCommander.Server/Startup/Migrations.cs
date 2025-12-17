using LANCommander.SDK.Migrations;
using LANCommander.SDK.Services;
using LANCommander.Server.Migrations;

namespace LANCommander.Server.Startup;

public static class Migrations
{
    public static WebApplicationBuilder AddMigrations(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<IMigration, CombineSettingsYaml>();
        builder.Services.AddScoped<IMigration, EncapsulateUserData>();

        return builder;
    }

    public static async Task<WebApplication> RunApplicationMigrationsAsync(this WebApplication app)
    {
        var migrationService = app.Services.GetRequiredService<MigrationService>();

        await migrationService.MigrateAsync();

        return app;
    }
}