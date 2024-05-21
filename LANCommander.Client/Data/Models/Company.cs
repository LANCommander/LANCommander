using System.Text.Json.Serialization;

namespace LANCommander.Client.Data.Models
{
    public class Company : BaseModel
    {
        public string Name { get; set; }

        [JsonIgnore]
        public virtual ICollection<Game> PublishedGames { get; set; }
        [JsonIgnore]
        public virtual ICollection<Game> DevelopedGames { get; set; }
    }
}
