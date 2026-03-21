using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Launcher.Data.Models
{
    [Table("Tools")]
    public class Tool : BaseModel
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public string? Notes { get; set; }
        
        public bool Installed { get; set; }
        public string? InstallDirectory { get; set; }
        public string? InstalledVersion { get; set; }
        public DateTime? InstalledOn { get; set; }
        public string? LatestVersion { get; set; }
        
        public virtual ICollection<Game>? Games { get; set; } = new List<Game>();
    }
}
