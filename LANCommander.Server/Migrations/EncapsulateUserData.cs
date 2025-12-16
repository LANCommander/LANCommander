using System.Reflection;
using LANCommander.SDK;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Migrations;
using Semver;

namespace LANCommander.Server.Migrations;

public class EncapsulateUserData(
    ILogger<EncapsulateUserData> logger) : IMigration
{
    public SemVersion Version => new(2, 0, 0);

    private string _oldConfigDirectory = String.Empty;
    
    public async Task ExecuteAsync()
    {
        var baseDirectory = Directory.GetCurrentDirectory();

        if (DirectoryHelper.IsDirectoryWritable(baseDirectory))
            _oldConfigDirectory = baseDirectory;
        else
            _oldConfigDirectory = AppPaths.GetAppDataPath();
        
        MoveOldPath("Backups");
        MoveOldPath("Media");
        MoveOldPath("Saves");
        MoveOldPath("Snippets");
        MoveOldPath("Upload");
        MoveOldPath("Uploads");
        MoveOldPath("LANCommander.db");
        MoveOldPath("Servers");
        MoveOldPath("Updates");
        MoveOldPath("Launcher");
        MoveOldPath("Logs");
    }

    private void MoveOldPath(string path)
    {
        try
        {
            var source = Path.Combine(_oldConfigDirectory, path);
            var destination = AppPaths.GetConfigPath(path);

            if (source == destination)
                return;
            
            logger.LogInformation($"Moving old config directory/file \"{path}\"");
            
            DirectoryHelper.MoveContents(source, destination);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while moving old config directory/file");
        }
    }
}