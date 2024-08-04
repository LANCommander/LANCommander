
using LANCommander.Server.Services;
using System.IO;
using System.IO.Compression;

namespace LANCommander.Server.UI.Components.FileManagerComponents.Sources
{
    public class FileManagerArchiveSource : IFileManagerSource
    {
        private ArchiveService ArchiveService;

        private Guid ArchiveId { get; set; }
        private FileManagerDirectory CurrentPath { get; set; }
        private IEnumerable<ZipArchiveEntry> ZipArchiveEntries { get; set; }
        public string DirectorySeparatorCharacter { get; set; } = Separator.ToString();

        private const char Separator = '/';

        public FileManagerArchiveSource(ArchiveService archiveService, Guid archiveId) {
            ArchiveService = archiveService;
            ArchiveId = archiveId;

            var task = ArchiveService.GetContents(ArchiveId);

            task.Wait();

            ZipArchiveEntries = task.Result;
        }

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

        private string GetParentPath(string path)
        {
            path = path.TrimEnd(Separator);

            var parts = path.Split(Separator);

            return String.Join(Separator, parts.Take(parts.Length - 1));
        }

        private bool IsDirectChild(string parentPath, string childPath) {
            var realParentPath = parentPath.TrimStart(Separator);

            return childPath != realParentPath && childPath.StartsWith(realParentPath) && childPath.TrimEnd(Separator).Substring(realParentPath.Length + 1).Split(Separator).Length == 1;
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

            root.Children = GetEntries(root.Path).Where(e => e is FileManagerDirectory).Select(e => e as FileManagerDirectory).ToHashSet();

            roots.Add(root);

            return roots;
        }

        private IEnumerable<IFileManagerEntry> GetEntries(string path)
        {
            var entries = new List<IFileManagerEntry>();

            if (!String.IsNullOrWhiteSpace(path))
            {
                foreach (var zipArchiveEntry in ZipArchiveEntries.Where(e => IsDirectChild(path, e.FullName)))
                {
                    if (zipArchiveEntry.FullName.EndsWith(Separator))
                    {
                        var directory = GetDirectory(zipArchiveEntry.FullName);

                        directory.Children = GetEntries(zipArchiveEntry.FullName).Where(e => e is FileManagerDirectory).Select(e => e as FileManagerDirectory).ToHashSet();

                        entries.Add(directory);
                    }
                    else
                    {
                        var file = GetFile(zipArchiveEntry.FullName);

                        entries.Add(file);
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
            var zipArchiveEntry = ZipArchiveEntries.FirstOrDefault(e => e.FullName == path);

            var directory = new FileManagerDirectory
            {
                Path = zipArchiveEntry.FullName,
                Name = GetName(zipArchiveEntry.FullName),
                ModifiedOn = zipArchiveEntry.LastWriteTime.UtcDateTime.ToLocalTime(),
                CreatedOn = zipArchiveEntry.LastWriteTime.UtcDateTime.ToLocalTime(),
                Size = zipArchiveEntry.Length,
                Parent = CurrentPath
            };

            return directory;
        }

        public FileManagerFile GetFile(string path)
        {
            var zipArchiveEntry = ZipArchiveEntries.FirstOrDefault(e => e.FullName == path);

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
