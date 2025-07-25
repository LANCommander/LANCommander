using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using System.Threading.Tasks;
using LANCommander.SDK.Models;
using LANCommander.SDK.Services;

namespace LANCommander.SDK.Helpers
{
    public static class ManifestHelper
    {
        public static readonly ILogger Logger;

        public const string ManifestFilename = "Manifest.yml";

        public static bool Exists(string installDirectory, Guid id)
        {
            var path = GetPath(installDirectory, id);

            return File.Exists(path);
        }

        public static T Read<T>(string installDirectory)
        {
            var source = Path.Combine(installDirectory, ManifestFilename);
            var yaml = File.ReadAllText(source);

            Logger?.LogTrace("Deserializing manifest");

            var manifest = Deserialize<T>(yaml);

            return manifest;
        }

        public static T Read<T>(string installDirectory, Guid id)
            where T : class
        {
            var source = GetPath(installDirectory, id);

            if (File.Exists(source))
            {
                var yaml = File.ReadAllText(source);

                Logger?.LogTrace("Deserializing manifest");

                var manifest = Deserialize<T>(yaml);

                return manifest;
            }

            return null;
        }

        public static async Task<T> ReadAsync<T>(string installDirectory)
        {
            var source = Path.Combine(installDirectory, ManifestFilename);
            var yaml = await File.ReadAllTextAsync(source);

            Logger?.LogTrace("Deserializing manifest from path {ManifestPath}", source);

            var manifest = Deserialize<T>(yaml);

            return manifest;
        }

        public static async Task<T> ReadAsync<T>(string installDirectory, Guid id)
            where T : class
        {
            var source = GetPath(installDirectory, id);

            if (File.Exists(source))
            {
                var yaml = await File.ReadAllTextAsync(source);

                Logger?.LogTrace("Deserializing manifest from {ManifestPath}", source);

                var manifest = Deserialize<T>(yaml);

                return manifest;
            }

            return null;
        }
        
        public static string Write<T>(T manifest, string installDirectory)
            where T : IKeyedModel
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

        public static async Task<string> WriteAsync<T>(T manifest, string installDirectory)
            where T : IKeyedModel
        {
            var destination = GetPath(installDirectory, manifest.Id);

            if (!Directory.Exists(Path.GetDirectoryName(destination)))
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

            Logger?.LogTrace("Attempting to write manifest to path {Destination}", destination);

            var yaml = Serialize(manifest);

            Logger?.LogTrace("Writing manifest file");

            await File.WriteAllTextAsync(destination, yaml);

            return destination;
        }

        public static bool TryDeserialize<T>(string serializedManifest, out T manifest)
        {
            try
            {
                manifest = Deserialize<T>(serializedManifest);

                return true;
            }
            catch
            {
                manifest = default(T);
                
                return false;
            }
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

        public static string GetPath(string installDirectory, Guid id)
        {
            return GameService.GetMetadataFilePath(installDirectory, id, ManifestFilename);
        }
    }
}
