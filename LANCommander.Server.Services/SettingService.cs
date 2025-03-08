using LANCommander.Server.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Services
{
    public class SettingService
    {
        public static string WorkingDirectory { get; set; } = "";
        private const string FileName = "Settings.yml";

        public static string SettingsFile
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(WorkingDirectory))
                    return Path.Combine(WorkingDirectory, FileName);
                else
                    return FileName;
            }
        }

        private static Models.Settings Settings { get; set; }

        public static Models.Settings LoadSettings()
        {
            if (File.Exists(SettingsFile))
            {
                var contents = File.ReadAllText(SettingsFile);

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

                File.WriteAllText(SettingsFile, serializer.Serialize(settings));

                Settings = settings;
            }
        }
    }
}
