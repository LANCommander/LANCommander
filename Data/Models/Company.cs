namespace LANCommander.Data.Models
{
    public class Company : BaseModel
    {
        public string Name { get; set; }

        public ICollection<Game> PublishedGames { get; set; }
        public ICollection<Game> DevelopedGames { get; set; }
    }
}
