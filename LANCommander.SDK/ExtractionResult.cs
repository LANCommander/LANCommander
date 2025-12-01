using System.Diagnostics.CodeAnalysis;

namespace LANCommander.SDK
{
    internal class ExtractionResult
    {
        [MemberNotNullWhen(true, nameof(Directory))]
        public bool Success { get; set; }

        [MemberNotNullWhen(true, nameof(Directory))]
        public bool Canceled { get; set; }
        
        public string? Directory { get; set; }

        public List<FileEntry> Files { get; internal set; } = [];

        public class FileEntry
        {
            public required string EntryPath { get; set; }
            public required string LocalPath { get; set; }
        }
    }
}
