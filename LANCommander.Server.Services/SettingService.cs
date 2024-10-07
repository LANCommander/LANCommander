using LANCommander.Server.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Services
{
    public class SettingService
    {
        private const string SettingsFilename = "Settings.yml";

        private static Models.Settings Settings { get; set; }

        public static Models.Settings LoadSettings()
        {
            if (File.Exists(SettingsFilename))
            {
                var contents = File.ReadAllText(SettingsFilename);

                var deserializer = new DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .WithNamingConvention(new PascalCaseNamingConvention())
                    .Build();

                Settings = deserializer.Deserialize<Models.Settings>(contents);
            }
            else
            {
                Settings = new Models.Settings();

                SaveSettings(Settings);
            }

            return Settings;
        }

        public static Models.Settings GetSettings(bool forceLoad = false)
        {
            if (Settings == null || forceLoad)
                Settings = LoadSettings();

            return Settings;
        }

        public static void SaveSettings(Models.Settings settings)
        {
            if (settings != null)
            {
                var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

                File.WriteAllText(SettingsFilename, serializer.Serialize(settings));

                Settings = settings;
            }
        }
    }
}
