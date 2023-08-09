using LANCommander.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Services
{
    public class SettingService
    {
        private const string SettingsFilename = "Settings.yml";

        public static LANCommanderSettings GetSettings()
        {
            if (File.Exists(SettingsFilename))
            {
                var contents = File.ReadAllText(SettingsFilename);

                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .Build();

                return deserializer.Deserialize<LANCommanderSettings>(contents);
            }
            else
            {
                var settings = new LANCommanderSettings
                {
                    Port = 1337,
                    Beacon = true,
                    DatabaseConnectionString = "Data Source=LANCommander.db;Cache=Shared",
                    Authentication = new LANCommanderAuthenticationSettings
                    {
                        TokenSecret = Guid.NewGuid().ToString(),
                        TokenLifetime = 30,
                        PasswordRequireNonAlphanumeric = false,
                        PasswordRequireLowercase = false,
                        PasswordRequireUppercase = false,
                        PasswordRequireDigit = true,
                        PasswordRequiredLength = 6
                    }
                };

                SaveSettings(settings);

                return settings;
            }
        }

        public static void SaveSettings(LANCommanderSettings settings)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            File.WriteAllText(SettingsFilename, serializer.Serialize(settings));
        }
    }
}
