using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LANCommander.Data.Models
{
    [Table("Tags")]
    public class Tag : BaseModel
    {
        public string Name { get; set; }

        public List<Game> Games { get; set; }
    }
}
