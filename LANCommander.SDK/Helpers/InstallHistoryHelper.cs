using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LANCommander.SDK.Services;
using LANCommander.SDK.Models;

namespace LANCommander.SDK.Helpers
{
    /// <summary>
    /// Reads and writes the record of which archives were extracted into an install directory and the
    /// files each one wrote. FileList.txt is kept as the union of every recorded archive so uninstall
    /// removes the files of all applied updates, not just the last one.
    /// </summary>
    public static class InstallHistoryHelper
    {
        public const string HistoryFilename = "InstallHistory.yml";
        public const string ArchivesDirectoryName = "Archives";
        public const string FileListFilename = "FileList.txt";

        public static StringComparer PathComparer => StringComparer.OrdinalIgnoreCase;

        public static bool Exists(string installDirectory, Guid gameId)
            => File.Exists(GameClient.GetMetadataFilePath(installDirectory, gameId, HistoryFilename));

        public static InstallHistory Read(string installDirectory, Guid gameId)
        {
            var path = GameClient.GetMetadataFilePath(installDirectory, gameId, HistoryFilename);

            if (!File.Exists(path))
                return new InstallHistory();

            return ManifestHelper.Deserialize<InstallHistory>(File.ReadAllText(path)) ?? new InstallHistory();
        }

        public static void Write(string installDirectory, Guid gameId, InstallHistory history)
        {
            var path = GameClient.GetMetadataFilePath(installDirectory, gameId, HistoryFilename);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            File.WriteAllText(path, ManifestHelper.Serialize(history));
        }

        public static string GetArchiveFileListPath(string installDirectory, Guid gameId, Guid archiveId)
            => Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, gameId), ArchivesDirectoryName, $"{archiveId}.txt");

        /// <summary>Normalizes an archive entry key for comparison: forward slashes, no leading slash.</summary>
        public static string NormalizePath(string path)
            => (path ?? string.Empty).Replace('\\', '/').TrimStart('/');

        public static IReadOnlyList<InstalledArchiveFile> ReadArchiveFiles(string installDirectory, Guid gameId, Guid archiveId)
        {
            var path = GetArchiveFileListPath(installDirectory, gameId, archiveId);

            if (!File.Exists(path))
                return Array.Empty<InstalledArchiveFile>();

            return ParseArchiveFiles(File.ReadAllLines(path));
        }

        public static IReadOnlyList<InstalledArchiveFile> ParseArchiveFiles(IEnumerable<string> lines)
        {
            var files = new List<InstalledArchiveFile>();

            foreach (var line in lines)
            {
                if (String.IsNullOrWhiteSpace(line))
                    continue;

                var parts = line.Split('|').Select(p => p.Trim()).ToArray();

                files.Add(new InstalledArchiveFile
                {
                    Path = NormalizePath(parts[0]),
                    Crc = parts.Length > 1 ? parts[1] : string.Empty,
                    Created = parts.Length > 2 && parts[2] == "C",
                });
            }

            return files;
        }

        public static string FormatArchiveFiles(IEnumerable<InstalledArchiveFile> files)
        {
            var builder = new StringBuilder();

            foreach (var file in files)
                builder.AppendLine($"{file.Path} | {file.Crc} | {(file.Created ? "C" : "M")}");

            return builder.ToString();
        }

        /// <summary>
        /// Records that an archive was extracted: writes its file list, adds or replaces its history
        /// entry, and rebuilds FileList.txt.
        /// </summary>
        public static void Record(string installDirectory, Guid gameId, AppliedArchiveInfo archive, IEnumerable<InstalledArchiveFile> files)
        {
            var listPath = GetArchiveFileListPath(installDirectory, gameId, archive.ArchiveId);

            Directory.CreateDirectory(Path.GetDirectoryName(listPath)!);

            // Re-extracting an archive that is already recorded (e.g. switching back to it) keeps the
            // original created/overwritten flags so a later rollback still removes what it first added
            var previous = ReadArchiveFiles(installDirectory, gameId, archive.ArchiveId)
                .ToDictionary(f => f.Path, f => f.Created, PathComparer);

            var merged = files.Select(f => new InstalledArchiveFile
            {
                Path = NormalizePath(f.Path),
                Crc = f.Crc,
                Created = f.Created || (previous.TryGetValue(NormalizePath(f.Path), out var created) && created),
            });

            File.WriteAllText(listPath, FormatArchiveFiles(merged));

            var history = Read(installDirectory, gameId);

            history.Archives.RemoveAll(a => a.ArchiveId == archive.ArchiveId);
            history.Archives.Add(new InstallHistoryEntry
            {
                ArchiveId = archive.ArchiveId,
                VersionId = archive.VersionId,
                Version = archive.Version,
                SortOrder = archive.SortOrder,
                AppliedOn = DateTime.UtcNow,
            });

            history.Archives = history.Archives.OrderBy(a => a.SortOrder).ThenBy(a => a.AppliedOn).ToList();

            Write(installDirectory, gameId, history);

            RebuildFileList(installDirectory, gameId, history);
        }

        /// <summary>Forgets the given archives and their file lists, then rebuilds FileList.txt.</summary>
        public static void Remove(string installDirectory, Guid gameId, IEnumerable<Guid> archiveIds)
        {
            var ids = archiveIds.ToHashSet();

            if (ids.Count == 0)
                return;

            var history = Read(installDirectory, gameId);

            history.Archives.RemoveAll(a => ids.Contains(a.ArchiveId));

            foreach (var id in ids)
            {
                var listPath = GetArchiveFileListPath(installDirectory, gameId, id);

                if (File.Exists(listPath))
                    File.Delete(listPath);
            }

            Write(installDirectory, gameId, history);

            RebuildFileList(installDirectory, gameId, history);
        }

        /// <summary>
        /// Rewrites FileList.txt as every file of every recorded archive, in the existing
        /// "path | CRC" format. Later archives' CRCs win.
        /// </summary>
        public static void RebuildFileList(string installDirectory, Guid gameId, InstallHistory history)
        {
            var union = new Dictionary<string, string>(PathComparer);
            var order = new List<string>();

            foreach (var entry in history.Archives.OrderBy(a => a.SortOrder).ThenBy(a => a.AppliedOn))
            {
                foreach (var file in ReadArchiveFiles(installDirectory, gameId, entry.ArchiveId))
                {
                    if (!union.ContainsKey(file.Path))
                        order.Add(file.Path);

                    union[file.Path] = file.Crc;
                }
            }

            var builder = new StringBuilder();

            foreach (var path in order)
                builder.AppendLine($"{path} | {union[path]}");

            var fileListPath = GameClient.GetMetadataFilePath(installDirectory, gameId, FileListFilename);

            Directory.CreateDirectory(Path.GetDirectoryName(fileListPath)!);

            File.WriteAllText(fileListPath, builder.ToString());
        }
    }
}
