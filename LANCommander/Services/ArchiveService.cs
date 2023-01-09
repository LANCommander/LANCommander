using LANCommander.Data;
using LANCommander.Data.Models;
using LANCommander.Models;
using System.IO.Compression;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace LANCommander.Services
{
    public class ArchiveService : BaseDatabaseService<Archive>
    {
        public ArchiveService(DatabaseContext dbContext, IHttpContextAccessor httpContextAccessor) : base(dbContext, httpContextAccessor)
        {
        }

        public static GameManifest ReadManifest(string objectKey)
        {
            var upload = Path.Combine("Upload", objectKey);

            string manifestContents = String.Empty;

            if (!System.IO.File.Exists(upload))
                throw new FileNotFoundException(upload);

            using (ZipArchive zip = ZipFile.OpenRead(upload))
            {
                var entry = zip.Entries.FirstOrDefault(e => e.FullName == "_manifest.yml");

                if (entry == null)
                    throw new FileNotFoundException("Manifest not found");

                using (StreamReader sr = new StreamReader(entry.Open()))
                {
                    manifestContents = sr.ReadToEnd();
                }
            }

            var deserializer = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(PascalCaseNamingConvention.Instance)
                .Build();


            var manifest = deserializer.Deserialize<GameManifest>(manifestContents);

            return manifest;
        }

        public static byte[] ReadFile(string objectKey, string path)
        {
            var upload = Path.Combine("Upload", objectKey);

            if (!System.IO.File.Exists(upload))
                throw new FileNotFoundException(upload);

            using (ZipArchive zip = ZipFile.OpenRead(upload))
            {
                var entry = zip.Entries.FirstOrDefault(e => e.FullName == path);

                if (entry == null)
                    throw new FileNotFoundException(path);

                using (var ms = new MemoryStream())
                {
                    entry.Open().CopyTo(ms);

                    return ms.ToArray();
                }
            }
        }
    }
}
