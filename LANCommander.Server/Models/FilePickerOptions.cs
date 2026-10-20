using LANCommander.Server.UI.Components;

namespace LANCommander.Server.Models
{
    public class FilePickerOptions
    {
        public Guid? ArchiveId { get; set; }
        public string Root { get; set; }
        public bool Select { get; set; }
        public bool Multiple { get; set; } = false;
        public Func<IFileManagerEntry, bool> EntryVisible { get; set; } = _ => true;
        public Func<IFileManagerEntry, bool> EntrySelectable { get; set; } = _ => true;

        /// <summary>
        /// What the caller will do with a picked path, e.g. the reference inserted into a script.
        /// Shown in the footer so OK is never a surprise.
        /// </summary>
        public Func<string, string> Describe { get; set; } = path => path;

        /// <summary>The footer's word for what OK does with the pick, e.g. "Inserting".</summary>
        public string Verb { get; set; } = "Selected";

        /// <summary>
        /// Said under the entries when some can't be picked. <c>{n}</c> becomes how many and what
        /// they are ("5 files"). Left unset, the dialog words it from what can be picked.
        /// </summary>
        public string? UnselectableHint { get; set; }
    }
}
