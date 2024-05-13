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

        public const string ManifestFilename = "Manifest.yml";

        public static bool Exists(string installDirectory, Guid gameId)
        {
            var path = GetPath(installDirectory, gameId);

            return File.Exists(path);
        }

        public static GameManifest Read(string installDirectory)
        {
            var source = Path.Combine(installDirectory, ManifestFilename);
            var yaml = File.ReadAllText(source);

            Logger?.LogTrace("Deserializing manifest");

            var manifest = Deserialize<GameManifest>(yaml);

            return manifest;
        }

        public static GameManifest Read(string installDirectory, Guid gameId)
        {
            var source = GetPath(installDirectory, gameId);
            var yaml = File.ReadAllText(source);

            Logger?.LogTrace("Deserializing manifest");

            var manifest = Deserialize<GameManifest>(yaml);

            return manifest;
        }

        public static string Write(GameManifest manifest, string installDirectory)
        {
            var destination = GetPath(installDirectory, manifest.Id);

            if (!Directory.Exists(Path.GetDirectoryName(destination)))
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

            Logger?.LogTrace("Attempting to write manifest to path {Destination}", destination);

            var yaml = Serialize(manifest);

            Logger?.LogTrace("Writing manifest file");

            File.WriteAllText(destination, yaml);

            return destination;
        }

        public static T Deserialize<T>(string serializedManifest)
        {
            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            return deserializer.Deserialize<T>(serializedManifest);
        }

        public static string Serialize<T>(T manifest)
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(new PascalCaseNamingConvention())
                .Build();

            Logger?.LogTrace("Serializing manifest");

            var yaml = serializer.Serialize(manifest);

            return yaml;
        }

        public static string GetPath(string installDirectory, Guid gameId)
        {
            return GameService.GetMetadataFilePath(installDirectory, gameId, ManifestFilename);
        }
    }
}
