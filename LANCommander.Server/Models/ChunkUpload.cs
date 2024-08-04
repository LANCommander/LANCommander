namespace LANCommander.Server.Models
{
    public class ChunkUpload
    {
        public long Start { get; set; }
        public long End { get; set; }
        public long Total { get; set; }
        public Guid Key { get; set; }
        public IFormFile File { get; set; }
    }
}
