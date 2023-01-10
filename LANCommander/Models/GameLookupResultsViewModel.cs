using LANCommander.Data.Models;

namespace LANCommander.Models
{
    public class GameLookupResultsViewModel
    {
        public string Search { get; set; }
        public IEnumerable<Game> Results { get; set; }
    }
}
