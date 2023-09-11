namespace LANCommander.Models
{
    public class FilePickerOptions
    {
        public Guid ArchiveId { get; set; }
        public string Root { get; set; }
        public bool Select { get; set; }
        public bool Multiple { get; set; } = false;
        public bool AllowDirectories { get; set; } = false;
    }
}
