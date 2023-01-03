namespace LANCommander.Data.Models
{
    public class Company : BaseModel
    {
        public string Name { get; set; }

        public virtual ICollection<Game> PublishedGames { get; set; }
        public virtual ICollection<Game> DevelopedGames { get; set; }
    }
}
