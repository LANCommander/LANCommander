using LANCommander.Server.UI.Components.FileManagerComponents;

namespace LANCommander.Server.Models
{
    public class FilePickerOptions
    {
        public Guid ArchiveId { get; set; }
        public string Root { get; set; }
        public bool Select { get; set; }
        public bool Multiple { get; set; } = false;
        public Func<IFileManagerEntry, bool> EntryVisible { get; set; } = _ => true;
        public Func<IFileManagerEntry, bool> EntrySelectable { get; set; } = _ => true;
    }
}
