﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization.NamingConventions;
using LANCommander.Launcher.Models;

namespace LANCommander.Launcher.Services
{
    public static class SettingService
    {
        private const string SettingsFilename = "Settings.yml";
        private static string SettingsFilePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, SettingsFilename);
            }
        }

        private static Settings Settings { get; set; }

        public static Settings LoadSettings()
        {
            if (File.Exists(SettingsFilePath))
            {
                var contents = File.ReadAllText(SettingsFilePath);

                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .IgnoreUnmatchedProperties()
                    .WithNamingConvention(new PascalCaseNamingConvention())
                    .Build();

                Settings = deserializer.Deserialize<Settings>(contents);
            }
            else
            {
                Settings = new Settings();

                SaveSettings(Settings);
            }

            return Settings;
        }

        public static Settings GetSettings(bool forceLoad = false)
        {
            if (Settings == null || forceLoad)
                Settings = LoadSettings();

            return Settings;
        }

        public static void SaveSettings()
        {
            SaveSettings(Settings);
        }

        public static void SaveSettings(Settings settings)
        {
            if (settings != null)
            {
                var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

                File.WriteAllText(SettingsFilePath, serializer.Serialize(settings));

                Settings = settings;
            }
        }
    }
}
