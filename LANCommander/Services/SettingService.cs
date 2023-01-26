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
                return new LANCommanderSettings();
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
