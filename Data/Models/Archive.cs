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

        public virtual Game Game { get; set; }

        public virtual Archive? LastVersion { get; set; }
    }
}
