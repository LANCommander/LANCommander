using LANCommander.SDK.Helpers;
using Microsoft.Extensions.Logging;
using Semver;
using System;
using System.Threading.Tasks;

namespace LANCommander.SDK.Migrations;

public abstract class FileSystemMigration(ILogger logger) : IMigration
{
    public abstract SemVersion Version { get; }

    public Task ExecuteAsync()
    {
        logger.LogInformation("Starting file system migration: {MigrationType}", GetType().Name);

        if (EnvironmentHelper.IsRunningInContainer() && !AppPaths.ConfigDirectoryIsMounted())
            throw new PlatformNotSupportedException(
                "Aborting migration to avoid data loss. Application is running in a container but config directory is not mounted.");

        logger.LogInformation("File system migration pre-move checks passed.");

        return ExecutePostMoveAsync();
    }

    public abstract Task ExecutePostMoveAsync();

    
}