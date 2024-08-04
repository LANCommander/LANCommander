using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using System.IO.Compression;

namespace LANCommander.Server.Extensions
{
    public static class ZipArchiveExtensions
    {
        public static ZipArchiveEntry CreateEntry(this ZipArchive archive, string fileName, byte[] data)
        {
            var entry = archive.CreateEntry(fileName, CompressionLevel.Fastest);

            using (var stream = entry.Open())
            {
                stream.Write(data, 0, data.Length);
            }

            return entry;
        }

        public static async Task<string> ReadAllTextAsync(this ZipArchive zip, string entryName)
        {
            var entry = zip.GetEntry(entryName);

            using (var reader = new StreamReader(entry.Open()))
            {
                return await reader.ReadToEndAsync();
            }
        }

        public static void ExtractEntry(this ZipArchive zip, string entryName, string destinationFileName, bool overwrite = false)
        {
            var entry = zip.GetEntry(entryName);

            entry.ExtractToFile(destinationFileName, overwrite);
        }
    }
}
