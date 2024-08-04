namespace LANCommander.Server.Models
{
    public class UserViewModel
    {
        public Guid Id { get; set; }
        public string UserName { get; set; }
        public IEnumerable<string> Roles { get; set; }
        public long SavesSize { get; set; }
        public bool Approved { get; set; }
    }
}
