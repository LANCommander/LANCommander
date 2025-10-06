using LANCommander.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Server.Services
{
    public class SettingService
    {
        public static string WorkingDirectory { get; set; } = "";
        private const string FileName = "Settings.Server.yml";

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
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
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

        public static void WriteSettings(Models.Settings settings)
        {
            if (settings == null)
                return;

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            File.WriteAllText(SettingsFile, serializer.Serialize(settings));
        }

        public static Models.Settings GetSettings(bool forceLoad = false)
        {
            if (Settings == null || forceLoad)
                Settings = LoadSettings();

            return Settings;
        }

        public static void SaveSettings(Models.Settings settings, IServiceProvider? serviceProvider = null)
        {
            WriteSettings(settings);
            Settings = settings;

            ReloadSettings(settings, serviceProvider);
        }

        private static void ReloadSettings(Models.Settings settings, IServiceProvider? serviceProvider)
        {
            if (serviceProvider == null || settings == null)
                return;

            if (serviceProvider.GetService<UserService>() is var service)
            {
                service!.Reconfigure(settings);
            }
        }
    }
}
