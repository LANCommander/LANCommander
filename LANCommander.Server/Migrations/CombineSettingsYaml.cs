using System.Reflection;
using LANCommander.SDK.Abstractions;
using LANCommander.SDK.Migrations;
using LANCommander.Server.Settings.Enums;
using LANCommander.Server.Settings.Models;
using Semver;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Migrations;

public class CombineSettingsYaml(SettingsProvider<Settings.Settings> settingsProvider) : IMigration
{
    public SemVersion Version => new(2, 0, 0);
    
    public async Task ExecuteAsync()
    {
        var oldConfigDirectory = Directory.GetCurrentDirectory();

        if (!IsDirectoryWritable(oldConfigDirectory))
        {
            var (company, product) = GetCompanyAndProduct();
            var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        
            var appDataPath = Path.Combine(userRoot, company, product);
        
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            oldConfigDirectory = appDataPath;
        }
        
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        
        var oldConfigPath = Path.Combine(oldConfigDirectory, SDK.Models.Settings.SETTINGS_FILE_NAME);

        if (File.Exists(oldConfigPath))
        {
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