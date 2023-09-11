namespace LANCommander.Models
{
    public class ArchiveFilePickerOptions
    {
        public Guid ArchiveId { get; set; }
        public bool Select { get; set; }
        public bool Multiple { get; set; } = false;
        public bool AllowDirectories { get; set; } = false;
    }
}
