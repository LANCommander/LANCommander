using System;
using System.Linq;
using System.Threading.Tasks;
using LANCommander.SDK.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Services;

public class MigrationService(IServiceProvider serviceProvider)
{
    public async Task MigrateAsync()
    {
        var migrations = serviceProvider.GetServices<IMigration>();

        foreach (var migration in migrations.OrderBy(m => m.Version))
            await migration.ExecuteAsync();
    }
}