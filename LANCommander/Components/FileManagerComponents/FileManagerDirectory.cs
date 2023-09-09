using System.IO.Compression;

namespace LANCommander.Components.FileManagerComponents
{
    public class FileManagerDirectory : FileManagerEntry
    {
        public bool IsExpanded { get; set; } = false;
        public bool HasChildren => Children != null && Children.Count > 0;
        public HashSet<FileManagerDirectory> Children { get; set; } = new HashSet<FileManagerDirectory>();

        public void PopulateChildren(IEnumerable<ZipArchiveEntry> entries)
        {
            var childPaths = entries.Where(e => e.FullName.StartsWith("/") && e.FullName.EndsWith('/'));
            var directChildren = childPaths.Where(p => p.FullName != Path && p.FullName.Substring(Path.Length + 1).TrimEnd('/').Split('/').Length == 1);

            foreach (var directChild in directChildren)
            {
                var child = new FileManagerDirectory()
                {
                    Path = directChild.FullName,
                    Name = directChild.FullName.Substring(Path.Length).TrimEnd('/')
                };

                child.PopulateChildren(entries);

                Children.Add(child);
            }
        }

        public void PopulateChildren(IEnumerable<string> entries)
        {
            var separator = System.IO.Path.DirectorySeparatorChar;
            var childPaths = entries.Where(e => e.StartsWith(Path));
            var directChildren = childPaths.Where(p => p != Path && p.Substring(Path.Length + 1).Split(separator).Length == 1);

            foreach (var directChild in directChildren)
            {
                if (!Children.Any(c => c.Path == directChild))
                {
                    var child = new FileManagerDirectory()
                    {
                        Path = directChild,
                        Name = directChild.Substring(Path.Length).TrimStart(separator),
                        Parent = this
                    };

                    child.PopulateChildren(entries);

                    Children.Add(child);
                }
            }
        }
    }
}
