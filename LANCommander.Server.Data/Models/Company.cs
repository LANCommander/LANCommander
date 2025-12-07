using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class Company : BaseTaxonomyModel
    {
        [JsonIgnore]
        public ICollection<Game> PublishedGames { get; set; }
        [JsonIgnore]
        public ICollection<Game> DevelopedGames { get; set; }
        [JsonIgnore]
        [NotMapped]
        public override ICollection<Game> Games { get; set; }
    }
}
