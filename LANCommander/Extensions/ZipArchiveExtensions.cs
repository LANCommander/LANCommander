using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;

namespace LANCommander.Extensions
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
    }
}
