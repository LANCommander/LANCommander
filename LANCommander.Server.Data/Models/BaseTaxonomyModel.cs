using System.Text.Json.Serialization;

namespace LANCommander.Server.Data.Models
{
    public abstract class BaseTaxonomyModel : BaseModel
    {
        public string Name { get; set; }
        [JsonIgnore]
        public ICollection<Game> Games { get; set; }
    }
}
