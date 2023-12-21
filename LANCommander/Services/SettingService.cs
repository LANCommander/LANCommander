using LANCommander.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Services
{
    public class SettingService
    {
        private const string SettingsFilename = "Settings.yml";

        private static LANCommanderSettings Settings { get; set; }

        public static LANCommanderSettings LoadSettings()
        {
            if (File.Exists(SettingsFilename))
            {
                var contents = File.ReadAllText(SettingsFilename);

                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .WithNamingConvention(new PascalCaseNamingConvention())
                    .Build();

                Settings = deserializer.Deserialize<LANCommanderSettings>(contents);
            }
            else
            {
                Settings = new LANCommanderSettings();

                SaveSettings(Settings);
            }

            return Settings;
        }

        public static LANCommanderSettings GetSettings(bool forceLoad = false)
        {
            if (Settings == null || forceLoad)
                Settings = LoadSettings();

            return Settings;
        }

        public static void SaveSettings(LANCommanderSettings settings)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            File.WriteAllText(SettingsFilename, serializer.Serialize(settings));

            Settings = settings;
        }
    }
}
