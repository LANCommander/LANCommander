using LANCommander.SDK.Migrations;
using LANCommander.SDK.Services;
using LANCommander.Server.Migrations;

namespace LANCommander.Server.Startup;

public static class Migrations
{
    public static WebApplicationBuilder AddMigrations(this WebApplicationBuilder builder)
    {
        // Individual settings file migrations
        builder.Services.AddScoped<IMigration, CombineSettingsYamlFromRoot>();
        builder.Services.AddScoped<IMigration, CombineSettingsServerYamlFromRoot>();
        builder.Services.AddScoped<IMigration, CombineSettingsYamlFromConfig>();
        builder.Services.AddScoped<IMigration, CombineSettingsServerYamlFromConfig>();

        // Database location migrations
        builder.Services.AddScoped<IMigration, SqliteDatabaseLocationMigration>();
        
        // Individual path migrations
        builder.Services.AddScoped<IMigration, MoveBackupsMigration>();
        builder.Services.AddScoped<IMigration, MoveMediaMigration>();
        builder.Services.AddScoped<IMigration, MoveSavesMigration>();
        builder.Services.AddScoped<IMigration, MoveSnippetsMigration>();
        builder.Services.AddScoped<IMigration, MoveUploadMigration>();
        builder.Services.AddScoped<IMigration, MoveUploadsMigration>();
        builder.Services.AddScoped<IMigration, MoveServersMigration>();
        builder.Services.AddScoped<IMigration, MoveUpdatesMigration>();
        builder.Services.AddScoped<IMigration, MoveLauncherMigration>();
        builder.Services.AddScoped<IMigration, MoveLogsMigration>();

        return builder;
    }

    public static async Task<WebApplication> RunApplicationMigrationsAsync(this WebApplication app)
    {
        var migrationService = app.Services.GetRequiredService<MigrationService>();

        await migrationService.MigrateAsync();

        return app;
    }
}