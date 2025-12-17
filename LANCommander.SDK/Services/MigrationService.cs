using System;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Semver;

namespace LANCommander.SDK.Services;

public class MigrationService(
    ILogger<MigrationService> logger,
    IServiceProvider serviceProvider)
{
    public async Task MigrateAsync()
    {
        try
        {
            var migrations = serviceProvider.GetServices<IMigration>();

            foreach (var migration in migrations.OrderBy(m => m.Version, SemVersion.SortOrderComparer))
            {
                if (!await migration.ShouldExecuteAsync())
                {
                    logger.LogInformation("Skipping migration {MigrationType} because ShouldExecuteAsync returned false", migration.GetType().Name);
                    continue;
                }

                if (!await migration.PerformPreChecksAsync())
                {
                    logger.LogWarning("Skipping migration {MigrationType} because PerformPreChecksAsync returned false", migration.GetType().Name);
                    continue;
                }

                try
                {
                    await migration.ExecuteAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Could not execute migration {MigrationType}", migration.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not run application migrations");
        }
    }
}