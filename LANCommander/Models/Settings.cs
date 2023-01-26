namespace LANCommander.Models
{
    public class LANCommanderSettings
    {
        public int Port { get; set; }
        public bool Beacon { get; set; }
        public string TokenSecret { get; set; }
        public int TokenLifetime { get; set; }
        public string DatabaseConnectionString { get; set; }
        public string IGDBClientId { get; set; }
        public string IGDBClientSecret { get; set; }
    }
}
