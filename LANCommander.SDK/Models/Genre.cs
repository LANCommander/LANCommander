using System.Collections.Generic;

namespace LANCommander.SDK.Models
{
    public class Genre : BaseModel
    {
        public string Name { get; set; }
        public virtual IEnumerable<Game> Games { get; set; }
    }
}
