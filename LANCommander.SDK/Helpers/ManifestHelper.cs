using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;

namespace LANCommander.SDK.Helpers
{
    public static class ManifestHelper
    {
        public static readonly ILogger Logger;

        public const string ManifestFilename = "_manifest.yml";

        public static GameManifest Read(string installDirectory)
        {
            var source = Path.Combine(installDirectory, ManifestFilename);
            var yaml = File.ReadAllText(source);

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            Logger.LogTrace("Deserializing manifest");

            var manifest = deserializer.Deserialize<GameManifest>(source);

            return manifest;
        }

        public static void Write(GameManifest manifest, string installDirectory)
        {
            var destination = Path.Combine(installDirectory, ManifestFilename);

            Logger.LogTrace("Attempting to write manifest to path {Destination}", destination);

            var serializer = new SerializerBuilder()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();

            Logger.LogTrace("Serializing manifest");

            var yaml = serializer.Serialize(manifest);

            Logger.LogTrace("Writing manifest file");

            File.WriteAllText(destination, yaml);
        }
    }
}
