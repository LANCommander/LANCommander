using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using Semver;
using System.Threading.Tasks;

namespace LANCommander.SDK.Migrations;

public abstract class FileSystemMigration(ILogger logger) : IMigration
{
    public abstract SemVersion Version { get; }

    protected ILogger Logger => logger;

    public Task<bool> PerformPreChecksAsync()
    {
        logger.LogInformation("Starting file system migration: {MigrationType}", GetType().Name);

        if (EnvironmentHelper.IsRunningInContainer() && !AppPaths.ConfigDirectoryIsMounted())
        {
            Logger.LogError("Aborting migration to avoid data loss. Application is running in a container but config directory is not mounted.");
            return Task.FromResult(false);
        }

        Logger.LogInformation("File system migration pre-move checks passed.");
        return Task.FromResult(true);
    }

    public abstract Task ExecuteAsync();

    public abstract Task<bool> ShouldExecuteAsync();
}