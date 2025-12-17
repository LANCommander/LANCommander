using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Migrations;
using Semver;

namespace LANCommander.Server.Migrations;

/// <summary>
/// Base class for migrations that move a single path from old config location to new location.
/// </summary>
public abstract class MovePathMigration(ILogger logger) : FileSystemMigration(logger)
{
    /// <summary>
    /// The name of the path/folder to move.
    /// </summary>
    protected abstract string PathName { get; }

    /// <summary>
    /// Gets the old configuration directory.
    /// </summary>
    protected string GetOldConfigDirectory()
    {
        var baseDirectory = Directory.GetCurrentDirectory();

        if (DirectoryHelper.IsDirectoryWritable(baseDirectory))
            return baseDirectory;
        
        return AppPaths.GetAppDataPath();
    }

    public override async Task ExecuteAsync()
    {
        var oldConfigDirectory = GetOldConfigDirectory();
        
        try
        {
            var source = Path.Combine(oldConfigDirectory, PathName);
            var destination = AppPaths.GetConfigPath(PathName);

            if (source == destination)
                return;

            Logger.LogInformation("Moving old config directory/file \"{Path}\"", PathName);

            if (Directory.Exists(source))
                DirectoryHelper.MoveContents(source, destination);
            else if (File.Exists(source))
            {
                var destinationDirectory = Path.GetDirectoryName(destination);
                if (destinationDirectory != null && !Directory.Exists(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                File.Move(source, destination);
            }
            else
                Logger.LogInformation("Skipping old directory/file \"{Path}\" because it doesn't exist.", PathName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while moving old config directory/file \"{Path}\"", PathName);
        }
    }

    public override async Task<bool> ShouldExecuteAsync()
    {
        var oldConfigDirectory = GetOldConfigDirectory();
        var source = Path.Combine(oldConfigDirectory, PathName);
        var destination = AppPaths.GetConfigPath(PathName);

        // Only execute if source exists and is different from destination
        if (source == destination)
            return false;

        return Directory.Exists(source) || File.Exists(source);
    }
}

/// <summary>
/// Migration for moving Backups folder from old config location to new location.
/// </summary>
public class MoveBackupsMigration(ILogger<MoveBackupsMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Backups";
}

/// <summary>
/// Migration for moving Media folder from old config location to new location.
/// </summary>
public class MoveMediaMigration(ILogger<MoveMediaMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Media";
}

/// <summary>
/// Migration for moving Saves folder from old config location to new location.
/// </summary>
public class MoveSavesMigration(ILogger<MoveSavesMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Saves";
}

/// <summary>
/// Migration for moving Snippets folder from old config location to new location.
/// </summary>
public class MoveSnippetsMigration(ILogger<MoveSnippetsMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Snippets";
}

/// <summary>
/// Migration for moving Upload folder from old config location to new location.
/// </summary>
public class MoveUploadMigration(ILogger<MoveUploadMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Upload";
}

/// <summary>
/// Migration for moving Uploads folder from old config location to new location.
/// </summary>
public class MoveUploadsMigration(ILogger<MoveUploadsMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Uploads";
}

/// <summary>
/// Migration for moving Servers folder from old config location to new location.
/// </summary>
public class MoveServersMigration(ILogger<MoveServersMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Servers";
}

/// <summary>
/// Migration for moving Updates folder from old config location to new location.
/// </summary>
public class MoveUpdatesMigration(ILogger<MoveUpdatesMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Updates";
}

/// <summary>
/// Migration for moving Launcher folder from old config location to new location.
/// </summary>
public class MoveLauncherMigration(ILogger<MoveLauncherMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Launcher";
}

/// <summary>
/// Migration for moving Logs folder from old config location to new location.
/// </summary>
public class MoveLogsMigration(ILogger<MoveLogsMigration> logger) : MovePathMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);
    protected override string PathName => "Logs";
}
