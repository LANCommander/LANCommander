using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Launcher.Data.Models
{
    [Table("Genres")]
    public class Genre : BaseTaxonomyModel
    {
        [JsonIgnore]
        public virtual ICollection<Game> Games { get; set; } = new List<Game>();
    }
}
