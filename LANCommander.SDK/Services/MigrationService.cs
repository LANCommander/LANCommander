using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Semver;

namespace LANCommander.SDK.Services;

public class MigrationService(
    ILogger<MigrationService> logger,
    IServiceProvider serviceProvider,
    MigrationHistoryService historyService)
{
    public async Task MigrateAsync()
    {
        try
        {
            var migrations = serviceProvider.GetServices<IMigration>();

            foreach (var migration in migrations.OrderBy(m => m.Version, SemVersion.SortOrderComparer))
            {
                var migrationName = migration.GetType().Name;
                var stopwatch = Stopwatch.StartNew();

                // Check if migration was already executed
                if (await historyService.HasMigrationBeenExecutedAsync(migration))
                {
                    logger.LogDebug("Skipping migration {MigrationType} because it was already executed", migrationName);
                    continue;
                }

                // Check if migration should execute
                var shouldExecute = await migration.ShouldExecuteAsync();
                if (!shouldExecute)
                {
                    stopwatch.Stop();
                    logger.LogInformation("Skipping migration {MigrationType} because ShouldExecuteAsync returned false", migrationName);
                    await historyService.RecordMigrationAsync(
                        migration,
                        MigrationStatus.SkippedNotApplicable,
                        stopwatch.ElapsedMilliseconds,
                        shouldExecute: false);
                    continue;
                }

                // Perform pre-checks
                var preChecksPassed = await migration.PerformPreChecksAsync();
                if (!preChecksPassed)
                {
                    stopwatch.Stop();
                    logger.LogWarning("Skipping migration {MigrationType} because PerformPreChecksAsync returned false", migrationName);
                    await historyService.RecordMigrationAsync(
                        migration,
                        MigrationStatus.SkippedPreChecksFailed,
                        stopwatch.ElapsedMilliseconds,
                        shouldExecute: true,
                        preChecksPassed: false);
                    continue;
                }

                // Execute the migration
                try
                {
                    await migration.ExecuteAsync();
                    stopwatch.Stop();

                    logger.LogInformation("Successfully executed migration {MigrationType} in {Duration}ms", migrationName, stopwatch.ElapsedMilliseconds);
                    await historyService.RecordMigrationAsync(
                        migration,
                        MigrationStatus.Executed,
                        stopwatch.ElapsedMilliseconds,
                        shouldExecute: true,
                        preChecksPassed: true);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    logger.LogError(ex, "Could not execute migration {MigrationType}", migrationName);
                    await historyService.RecordMigrationAsync(
                        migration,
                        MigrationStatus.Failed,
                        stopwatch.ElapsedMilliseconds,
                        shouldExecute: true,
                        preChecksPassed: true,
                        exception: ex);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not run application migrations");
        }
    }
}