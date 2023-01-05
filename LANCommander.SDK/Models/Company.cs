using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    public class Company : BaseModel
    {
        public string Name { get; set; }
        public virtual IEnumerable<Game> PublishedGames { get; set; }
        public virtual IEnumerable<Game> DevelopedGames { get; set; }
    }
}
