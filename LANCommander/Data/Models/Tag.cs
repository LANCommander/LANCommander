using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Data.Models
{
    [Table("Tags")]
    public class Tag : BaseModel
    {
        public string Name { get; set; }

        [JsonIgnore]
        public virtual List<Game> Games { get; set; }
    }
}
