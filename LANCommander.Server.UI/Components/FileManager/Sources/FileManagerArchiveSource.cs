﻿
using System.IO.Compression;
using LANCommander.SDK.Services;

namespace LANCommander.Server.UI.Components
{
    public class FileManagerArchiveSource : IFileManagerSource
    {
        private IArchiveClient _archiveClient;

        private Guid ArchiveId { get; set; }
        private FileManagerDirectory CurrentPath { get; set; }
        private IEnumerable<ZipArchiveEntry> ZipArchiveEntries { get; set; }

        /// <summary>
        /// Every file and folder in the archive keyed by full path (folders end in a separator).
        /// Many zip tools only write file entries, so folders are also derived from file paths;
        /// those have no entry of their own and map to null.
        /// </summary>
        private Dictionary<string, ZipArchiveEntry?> Paths { get; } = new(StringComparer.Ordinal);

        /// <summary>Child paths of every folder, keyed by the folder's path (empty for the root).</summary>
        private Dictionary<string, List<string>> Children { get; } = new(StringComparer.Ordinal);

        public string DirectorySeparatorCharacter { get; set; } = Separator.ToString();

        private const char Separator = '/';

        public FileManagerArchiveSource(IArchiveClient archiveClient, Guid archiveId) {
            _archiveClient = archiveClient;
            ArchiveId = archiveId;

            var task = _archiveClient.GetContentsAsync(ArchiveId);

            task.Wait();

            ZipArchiveEntries = task.Result;

            foreach (var entry in ZipArchiveEntries)
            {
                Paths[entry.FullName] = entry;

                for (var index = entry.FullName.IndexOf(Separator); index >= 0 && index < entry.FullName.Length - 1; index = entry.FullName.IndexOf(Separator, index + 1))
                    Paths.TryAdd(entry.FullName.Substring(0, index + 1), null);
            }

            foreach (var path in Paths.Keys.Order(StringComparer.Ordinal))
            {
                var parentPath = GetParentPath(path);

                if (!Children.TryGetValue(parentPath, out var children))
                    Children[parentPath] = children = new List<string>();

                children.Add(path);
            }
        }

        /// <summary>How many entries, files and folders, the archive holds.</summary>
        public int EntryCount => ZipArchiveEntries.Count();

        public FileManagerDirectory CreateDirectory(string name)
        {
            throw new NotImplementedException();
        }

        public void DeleteEntry(IFileManagerEntry entry)
        {
            throw new NotImplementedException();
        }

        public FileManagerDirectory ExpandNode(FileManagerDirectory node)
        {
            return node;
        }

        public FileManagerDirectory GetCurrentPath()
        {
            return CurrentPath;
        }

        public void SetCurrentPath(FileManagerDirectory path)
        {
            CurrentPath = path;
        }

        private string GetName(string path)
        {
            return path.TrimEnd(Separator).Split(Separator).Last();
        }

        /// <summary>The archive path of the folder holding <paramref name="path"/>, with a trailing separator; empty at the root.</summary>
        private static string GetParentPath(string path)
        {
            var index = path.TrimEnd(Separator).LastIndexOf(Separator);

            return index < 0 ? String.Empty : path.Substring(0, index + 1);
        }

        /// <summary>The tree root is "/", archive entries have no leading separator.</summary>
        private static string ToArchivePath(string path)
        {
            path = path.TrimStart(Separator);

            return path.Length == 0 || path.EndsWith(Separator) ? path : path + Separator;
        }

        public IEnumerable<FileManagerDirectory> GetDirectoryTree()
        {
            var roots = new List<FileManagerDirectory>();

            var root = new FileManagerDirectory
            {
                Name = "/",
                Path = "/",
                IsExpanded = true
            };

            SetCurrentPath(root);

            root.Children = GetEntries(root.Path).OfType<FileManagerDirectory>().ToHashSet();

            roots.Add(root);

            return roots;
        }

        private IEnumerable<IFileManagerEntry> GetEntries(string path)
        {
            var entries = new List<IFileManagerEntry>();

            if (!String.IsNullOrWhiteSpace(path))
            {
                Children.TryGetValue(ToArchivePath(path), out var childPaths);

                foreach (var childPath in childPaths ?? [])
                {
                    if (childPath.EndsWith(Separator))
                    {
                        var directory = GetDirectory(childPath);

                        directory.Children = GetEntries(childPath).OfType<FileManagerDirectory>().ToHashSet();

                        entries.Add(directory);
                    }
                    else
                    {
                        entries.Add(GetFile(childPath));
                    }
                }
            }

            return entries;
        }

        public IEnumerable<IFileManagerEntry> GetEntries()
        {
            if (CurrentPath != null && !String.IsNullOrWhiteSpace(CurrentPath.Path))
            {
                return GetEntries(CurrentPath.Path);
            }

            return new List<IFileManagerEntry>();
        }

        public string GetEntryName(IFileManagerEntry entry)
        {
            return GetName(entry.Path);
        }

        public FileManagerDirectory GetDirectory(string path)
        {
            path = ToArchivePath(path);

            Paths.TryGetValue(path, out var zipArchiveEntry);

            var modifiedOn = zipArchiveEntry?.LastWriteTime.UtcDateTime.ToLocalTime() ?? default;

            return new FileManagerDirectory
            {
                Path = path,
                Name = GetName(path),
                ModifiedOn = modifiedOn,
                CreatedOn = modifiedOn,
                Size = zipArchiveEntry?.Length ?? 0,
                Parent = CurrentPath
            };
        }

        public FileManagerFile GetFile(string path)
        {
            var zipArchiveEntry = Paths[path]!;

            var file = new FileManagerFile
            {
                Path = zipArchiveEntry.FullName,
                Name = zipArchiveEntry.Name,
                ModifiedOn = zipArchiveEntry.LastWriteTime.UtcDateTime.ToLocalTime(),
                CreatedOn = zipArchiveEntry.LastWriteTime.UtcDateTime.ToLocalTime(),
                Size = zipArchiveEntry.Length,
                Parent = CurrentPath
            };

            return file;
        }
    }
}
