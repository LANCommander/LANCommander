using System.Reflection;
using LANCommander.SDK.Migrations;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Semver;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Migrations;

public class CombineSettingsYaml(SettingsProvider<Settings.Settings> settingsProvider, ILogger<CombineSettingsYaml> logger) : FileSystemMigration(logger)
{
    public override SemVersion Version => new(2, 0, 0);

    public override async Task ExecutePostMoveAsync()
    {
        var oldConfigDirectory = Directory.GetCurrentDirectory();
        if (await TryMigrateSettingsFromLocation(oldConfigDirectory, SDK.Models.Settings.SETTINGS_FILE_NAME))
        {
            return;
        }

        if (await TryMigrateSettingsFromLocation(oldConfigDirectory, "Settings.Server.yml"))
        {
            return;
        }

        if (await TryMigrateSettingsFromLocation(Path.Combine(oldConfigDirectory, "config"), SDK.Models.Settings.SETTINGS_FILE_NAME))
        {
            return;
        }

        await TryMigrateSettingsFromLocation(Path.Combine(oldConfigDirectory, "config"), "Settings.Server.yml");
    }

    private async Task<bool> TryMigrateSettingsFromLocation(string oldConfigDirectory, string settingsFileName)
    {
        logger.LogInformation("Preparing to migrate settinngs from {OldConfigDirectory}", oldConfigDirectory);

        if (!IsDirectoryWritable(oldConfigDirectory))
        {
            logger.LogInformation("Old config directory is not writable, attempting to locate settings in user profile.");

            var (company, product) = GetCompanyAndProduct();
            var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var appDataPath = Path.Combine(userRoot, company, product);

            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            oldConfigDirectory = appDataPath;

            logger.LogInformation("Located old config directory at {OldConfigDirectory}", oldConfigDirectory);
        }

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();

        var oldConfigPath = Path.Combine(oldConfigDirectory, settingsFileName);

        if (!File.Exists(oldConfigPath))
        {
            logger.LogInformation("No old settings file found at {OldConfigPath}", oldConfigPath);
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

        settingsProvider.Update(s =>
        {
            s.Server = convertedSettings;
        });

        logger.LogInformation("Successfully migrated settings from {OldConfigPath}", oldConfigPath);

        return true;
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

    private (string? Company, string? Product) GetCompanyAndProduct()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        return (company, product);
    }

    private class LegacySettings
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