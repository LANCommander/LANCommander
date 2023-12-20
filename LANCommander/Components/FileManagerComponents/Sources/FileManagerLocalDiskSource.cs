
using LANCommander.Services;
using NuGet.Packaging;
using SharpCompress.Common;

namespace LANCommander.Components.FileManagerComponents.Sources
{
    public class FileManagerLocalDiskSource : IFileManagerSource
    {
        private FileManagerDirectory CurrentPath { get; set; }

        public FileManagerLocalDiskSource(string path) {
            SetCurrentPath(GetDirectory(path));

            //InitTree();
        }

        public FileManagerDirectory GetCurrentPath()
        {
            return CurrentPath;
        }

        public void SetCurrentPath(FileManagerDirectory path)
        {
            CurrentPath = path;
        }

        public FileManagerDirectory ExpandNode(FileManagerDirectory node)
        {
            node.IsExpanded = true;

            foreach (var child in node.Children)
            {
                var paths = Directory.EnumerateDirectories(child.Path, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 1
                }).ToArray();

                child.Children = GetChildren(child, paths).ToHashSet();
            }

            return node;
        }

        private IEnumerable<FileManagerDirectory> GetChildren(FileManagerDirectory parent, IEnumerable<string> entries)
        {
            var childPaths = entries.Where(e => e.StartsWith(parent.Path));
            var directChildren = childPaths.Where(cp => Path.GetDirectoryName(cp) == parent.Path).ToArray();

            return directChildren.Select(dc =>
            {
                var child = new FileManagerDirectory
                {
                    Path = dc,
                    Parent = parent
                };

                child.Name = GetEntryName(child);

                return child;
            });
        }

        public IEnumerable<IFileManagerEntry> GetEntries()
        {
            var entries = new List<IFileManagerEntry>();

            if (CurrentPath != null && !String.IsNullOrWhiteSpace(CurrentPath.Path))
            {
                var filePaths = Directory.EnumerateFileSystemEntries(CurrentPath.Path);

                foreach (var filePath in filePaths)
                {
                    // Is directory
                    if (Directory.Exists(filePath))
                    {
                        try
                        {
                            var directory = GetDirectory(filePath);

                            entries.Add(directory);
                        }
                        catch { }
                    }
                    else
                    {
                        try
                        {
                            var file = GetFile(filePath);

                            entries.Add(file);
                        }
                        catch { }
                    }
                }
            }

            return entries;
        }

        public FileManagerDirectory GetDirectory(string path)
        {
            var info = new DirectoryInfo(path);
            var directory = new FileManagerDirectory
            {
                Path = path,
                ModifiedOn = info.LastWriteTime,
                CreatedOn = info.LastWriteTime,
                Parent = CurrentPath
            };

            directory.Name = GetEntryName(directory);

            return directory;
        }

        public FileManagerFile GetFile(string path)
        {
            var info = new FileInfo(path);
            var file = new FileManagerFile
            {
                Path = path,
                Name = Path.GetFileName(path),
                ModifiedOn = info.LastWriteTime,
                CreatedOn = info.LastWriteTime,
                Size = info.Length,
                Parent = CurrentPath
            };

            return file;
        }

        public string GetEntryName(IFileManagerEntry entry)
        {
            if (!String.IsNullOrWhiteSpace(entry.Name) && Directory.Exists(entry.Path))
                return entry.Path.TrimEnd(Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar).Last();
            else
                return Path.GetFileName(entry.Path);
        }

        public FileManagerDirectory CreateDirectory(string name)
        {
            var path = Path.Combine(CurrentPath.Path, name);

            Directory.CreateDirectory(path);

            return GetDirectory(path);
        }

        public void DeleteEntry(IFileManagerEntry entry)
        {
            if (entry is FileManagerDirectory)
                Directory.Delete(entry.Path);
            else if (entry is FileManagerFile)
                File.Delete(entry.Path);
        }

        public IEnumerable<FileManagerDirectory> GetDirectoryTree()
        {
            var roots = new List<FileManagerDirectory>();
            var settings = SettingService.GetSettings();

            #if WINDOWS
            var drives = settings.GetDrives();

            roots.AddRange(drives.Where(d => (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed) && d.IsReady).Select(d =>
            {
                var root = new FileManagerDirectory
                {
                    Path = d.RootDirectory.FullName,
                    Name = d.Name,
                };

                var paths = Directory.EnumerateDirectories(root.Path, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 1
                }).ToArray();

                root.Children = GetChildren(root, paths).ToHashSet();

                return root;
            }));
            #else
            roots.Add(new FileManagerDirectory {
                Name = "/",
                Path = "/",
                IsExpanded = true
            });
            #endif

            return roots;
        }
    }
}
