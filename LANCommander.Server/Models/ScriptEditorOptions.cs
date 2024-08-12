using LANCommander.SDK.Enums;

namespace LANCommander.Server.Models
{
    public class ScriptEditorOptions
    {
        public Guid ScriptId { get; set; }
        public Guid GameId { get; set; }
        public Guid RedistributableId { get; set; }
        public Guid ServerId { get; set; }
        public Guid ArchiveId { get; set; }
        public IEnumerable<ScriptType> AllowedTypes { get; set; }
    }
}
