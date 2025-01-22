using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public class Company : BaseModel
    {
        public string Name { get; set; }

        [JsonIgnore]
        public ICollection<Game> PublishedGames { get; set; } = new List<Game>();
        [JsonIgnore]
        public ICollection<Game> DevelopedGames { get; set; } = new List<Game>();
    }
}
