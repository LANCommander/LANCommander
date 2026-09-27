using LANCommander.Server.Settings.Models;

namespace LANCommander.Server.Models
{
    public class ProviderButtonModel
    {
        public required AuthenticationProvider Provider { get; set; }
        public required string Text { get; set; }
    }
}
