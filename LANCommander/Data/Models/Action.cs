using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Actions")]
    public class Action : BaseModel
    {
        public string Name { get; set; }
        public string Arguments { get; set; }
        public string Path { get; set; }
        public string WorkingDirectory { get; set; }
        public bool PrimaryAction { get; set; }

        public virtual Game Game { get; set; }
    }
}
