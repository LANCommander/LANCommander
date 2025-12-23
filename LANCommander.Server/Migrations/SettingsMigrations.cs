using System.Reflection;
using LANCommander.SDK;
using LANCommander.SDK.Migrations;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Microsoft.Data.Sqlite;
using Semver;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Migrations;

/// <summary>
/// Base class for migrations that combine old settings YAML files into the new unified settings format.
/// </summary>
public abstract class SettingsMigration(SettingsProvider<Settings.Settings> settingsProvider, ILogger logger) : FileSystemMigration(logger)
{
    protected SettingsProvider<Settings.Settings> SettingsProvider => settingsProvider;

    /// <summary>
    /// The directory to search for the settings file (relative to a base directory).
    /// </summary>
    protected abstract string RelativeDirectory { get; }

    /// <summary>
    /// The name of the settings file to migrate.
    /// </summary>
    protected abstract string SettingsFileName { get; }

    public override SemVersion Version => new(2, 0, 0);

    public override async Task ExecuteAsync()
    {
        var baseDirectory = Directory.GetCurrentDirectory();
        var searchDirectory = string.IsNullOrEmpty(RelativeDirectory) 
            ? baseDirectory 
            : Path.Combine(baseDirectory, RelativeDirectory);

        await TryMigrateSettingsFromLocation(searchDirectory, SettingsFileName);
    }

    public override async Task<bool> ShouldExecuteAsync()
    {
        var baseDirectory = Directory.GetCurrentDirectory();
        var searchDirectory = string.IsNullOrEmpty(RelativeDirectory)
            ? baseDirectory
            : Path.Combine(baseDirectory, RelativeDirectory);

        return FileExists(searchDirectory, SettingsFileName);
    }

    protected bool IsDirectoryWritable(string path)
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

    protected (string? Company, string? Product) GetCompanyAndProduct()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        return (company, product);
    }

    protected bool FileExists(string directory, string fileName)
    {
        var filePath = Path.Combine(directory, fileName);
        return File.Exists(filePath);
    }

    protected async Task<bool> TryMigrateSettingsFromLocation(string oldConfigDirectory, string settingsFileName)
    {
        Logger.LogInformation("Preparing to migrate settings from {OldConfigDirectory} using file {SettingsFileName}", oldConfigDirectory, settingsFileName);

        if (!IsDirectoryWritable(oldConfigDirectory))
        {
            Logger.LogInformation("Old config directory is not writable, attempting to locate settings in user profile.");

            var (company, product) = GetCompanyAndProduct();
            var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var appDataPath = Path.Combine(userRoot, company, product);

            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            oldConfigDirectory = appDataPath;

            Logger.LogInformation("Located old config directory at {OldConfigDirectory}", oldConfigDirectory);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        var oldConfigPath = Path.Combine(oldConfigDirectory, settingsFileName);

        if (!File.Exists(oldConfigPath))
        {
            Logger.LogInformation("No old settings file found at {OldConfigPath}", oldConfigPath);
            return false;
        }

        var convertedSettings = deserializer.Deserialize<ServerSettings>(await File.ReadAllTextAsync(oldConfigPath));
        var oldSettings = deserializer.Deserialize<LegacySettings>(await File.ReadAllTextAsync(oldConfigPath));

        convertedSettings.Http.Port = oldSettings.Port;
        convertedSettings.Http.SSLPort = oldSettings.SSLPort;
        convertedSettings.Http.CertificatePath = oldSettings.CertificatePath;
        convertedSettings.Http.CertificatePassword = oldSettings.CertificatePassword;
        convertedSettings.Http.UseSSL = oldSettings.UseSSL;
        convertedSettings.Database.Provider = oldSettings.DatabaseProvider;
        convertedSettings.Database.ConnectionString = oldSettings.DatabaseConnectionString;
        convertedSettings.IGDB.ClientId = oldSettings.IGDBClientId;
        convertedSettings.IGDB.ClientSecret = oldSettings.IGDBClientSecret;
        convertedSettings.GameServers = oldSettings.Servers;

        SettingsProvider.Update(s =>
        {
            s.Server = convertedSettings;
        });

        Logger.LogInformation("Successfully migrated settings from {OldConfigPath}", oldConfigPath);

        return true;
    }

    protected class LegacySettings
    {
        public int Port { get; set; }
        public DatabaseProvider DatabaseProvider { get; set; }
        public string DatabaseConnectionString { get; set; }
        public string IGDBClientId { get; set; }
        public string IGDBClientSecret { get; set; }
        public bool UseSSL { get; set; }
        public string CertificatePath { get; set; }
        public string CertificatePassword { get; set; }
        public int SSLPort { get; set; }
        public GameServerSettings Servers { get; set; }
    }
}

/// <summary>
/// Migration for combining old settings YAML file (Settings.yml) in root directory.
/// </summary>
public class CombineSettingsYamlFromRoot(SettingsProvider<Settings.Settings> settingsProvider, ILogger<CombineSettingsYamlFromRoot> logger) : SettingsMigration(settingsProvider, logger)
{
    protected override string RelativeDirectory => string.Empty;
    protected override string SettingsFileName => SDK.Models.Settings.SETTINGS_FILE_NAME;
}

/// <summary>
/// Migration for combining old settings YAML file (Settings.Server.yml) in root directory.
/// </summary>
public class CombineSettingsServerYamlFromRoot(SettingsProvider<Settings.Settings> settingsProvider, ILogger<CombineSettingsServerYamlFromRoot> logger) : SettingsMigration(settingsProvider, logger)
{
    protected override string RelativeDirectory => string.Empty;
    protected override string SettingsFileName => "Settings.Server.yml";
}

/// <summary>
/// Migration for combining old settings YAML file (Settings.yml) in config subdirectory.
/// </summary>
public class CombineSettingsYamlFromConfig(SettingsProvider<Settings.Settings> settingsProvider, ILogger<CombineSettingsYamlFromConfig> logger) : SettingsMigration(settingsProvider, logger)
{
    protected override string RelativeDirectory => "config";
    protected override string SettingsFileName => SDK.Models.Settings.SETTINGS_FILE_NAME;
}

/// <summary>
/// Migration for combining old settings YAML file (Settings.Server.yml) in config subdirectory.
/// </summary>
public class CombineSettingsServerYamlFromConfig(SettingsProvider<Settings.Settings> settingsProvider, ILogger<CombineSettingsServerYamlFromConfig> logger) : SettingsMigration(settingsProvider, logger)
{
    protected override string RelativeDirectory => "config";
    protected override string SettingsFileName => "Settings.Server.yml";
}
