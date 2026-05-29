using System.Runtime.InteropServices;

namespace LANCommander.UI.Components
{
    public class FileManagerLocalDiskSource : IFileManagerSource
    {
        private FileManagerDirectory CurrentPath { get; set; }
        public string DirectorySeparatorCharacter { get; set; } = Path.DirectorySeparatorChar.ToString();

        public FileManagerLocalDiskSource(string path) {
            SetCurrentPath(GetDirectory(path));
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
                var dirInfo = new DirectoryInfo(CurrentPath.Path);

                try
                {
                    foreach (var info in dirInfo.EnumerateFileSystemInfos())
                    {
                        try
                        {
                            if (info is DirectoryInfo di)
                            {
                                entries.Add(new FileManagerDirectory
                                {
                                    Path = di.FullName,
                                    Name = di.Name,
                                    ModifiedOn = di.LastWriteTime,
                                    CreatedOn = di.CreationTime,
                                    Parent = CurrentPath
                                });
                            }
                            else if (info is FileInfo fi)
                            {
                                entries.Add(new FileManagerFile
                                {
                                    Path = fi.FullName,
                                    Name = fi.Name,
                                    ModifiedOn = fi.LastWriteTime,
                                    CreatedOn = fi.CreationTime,
                                    Size = fi.Length,
                                    Parent = CurrentPath
                                });
                            }
                        }
                        catch { }
                    }
                }
                catch { }
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
            if (!String.IsNullOrWhiteSpace(entry.Name))
                return entry.Name;

            return Path.GetFileName(entry.Path.TrimEnd(Path.DirectorySeparatorChar));
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

        private FileManagerDirectory CreateRootDirectory(string name, string path)
        {
            var root = new FileManagerDirectory
            {
                Name = name,
                Path = path,
            };

            try
            {
                var paths = Directory.EnumerateDirectories(root.Path, "*", new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 1
                }).ToArray();

                root.Children = GetChildren(root, paths).ToHashSet();
            }
            catch { }

            return root;
        }

        public IEnumerable<FileManagerDirectory> GetDirectoryTree()
        {
            var roots = new List<FileManagerDirectory>();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var drives = DriveInfo.GetDrives();

                roots.AddRange(drives
                    .Where(d => (d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed) && d.IsReady)
                    .Select(d => CreateRootDirectory(d.Name.TrimEnd('\\'), d.RootDirectory.FullName)));

                var specialFolders = new (string Name, Environment.SpecialFolder Folder)[]
                {
                    ("Desktop", Environment.SpecialFolder.Desktop),
                    ("Documents", Environment.SpecialFolder.MyDocuments),
                };

                foreach (var (name, folder) in specialFolders)
                {
                    var path = Environment.GetFolderPath(folder);

                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        roots.Add(CreateRootDirectory(name, path));
                }

                var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

                if (Directory.Exists(downloadsPath))
                    roots.Add(CreateRootDirectory("Downloads", downloadsPath));
            }
            else
            {
                roots.Add(CreateRootDirectory("/", "/"));

                var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(homePath) && Directory.Exists(homePath))
                {
                    roots.Add(CreateRootDirectory("Home", homePath));

                    foreach (var subFolder in new[] { "Desktop", "Documents", "Downloads" })
                    {
                        var subPath = Path.Combine(homePath, subFolder);

                        if (Directory.Exists(subPath))
                            roots.Add(CreateRootDirectory(subFolder, subPath));
                    }
                }

                foreach (var (name, path) in new[] { ("/etc", "/etc"), ("/tmp", "/tmp") })
                {
                    if (Directory.Exists(path))
                        roots.Add(CreateRootDirectory(name, path));
                }
            }

            return roots;
        }
    }
}
