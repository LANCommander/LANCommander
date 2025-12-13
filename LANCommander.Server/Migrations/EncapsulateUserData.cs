using System.Reflection;
using LANCommander.SDK;
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

        if (IsDirectoryWritable(baseDirectory))
        {
            _oldConfigDirectory = baseDirectory;
        }
        else
        {
            var (company, product) = GetCompanyAndProduct();
            var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
            var appDataPath = Path.Combine(userRoot, company, product);
        
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            _oldConfigDirectory = appDataPath;
        }
        
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
            logger.LogInformation($"Moving old config directory/file \"{path}\"");

            if (Directory.Exists(Path.Combine(_oldConfigDirectory, path)))
                Directory.Move(Path.Combine(_oldConfigDirectory, path), AppPaths.GetConfigPath(path));
            else if (File.Exists(Path.Combine(_oldConfigDirectory, path)))
                File.Move(Path.Combine(_oldConfigDirectory, path), AppPaths.GetConfigPath(path));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while moving old config directory/file");
        }
    }
    
    private bool IsDirectoryWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);

            var probeFile = Path.Combine(path, $".writetest.{Guid.NewGuid():N}.tmp");

            using (var fs = new FileStream(probeFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                fs.WriteByte(0);
            }

            File.Delete(probeFile);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private(string? Company, string? Product) GetCompanyAndProduct()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        
        return (company, product);
    }
}