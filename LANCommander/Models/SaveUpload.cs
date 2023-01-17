namespace LANCommander.Models
{
    public class SaveUpload
    {
        public Guid GameId { get; set; }
        public IFormFile File { get; set; }
    }
}
