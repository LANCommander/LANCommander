using System.Text.Json.Serialization;

namespace LANCommander.Launcher.Data.Models
{
    public class Company : BaseModel
    {
        public string Name { get; set; }

        [JsonIgnore]
        public virtual ICollection<Game> PublishedGames { get; set; } = new List<Game>();
        [JsonIgnore]
        public virtual ICollection<Game> DevelopedGames { get; set; } = new List<Game>();
    }
}
