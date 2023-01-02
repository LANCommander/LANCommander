using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    public class Archive : BaseModel
    {
        public string? Changelog { get; set; }

        public string ObjectKey { get; set; }

        [Required]
        public string Version { get; set; }

        public Game Game { get; set; }

        public Archive? LastVersion { get; set; }

        [NotMapped]
        public IFormFile UploadedFile { get; set; }
    }
}
