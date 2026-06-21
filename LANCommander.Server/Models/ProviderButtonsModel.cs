using LANCommander.Server.Settings.Models;

namespace LANCommander.Server.Models
{
    public class ProviderButtonsModel
    {
        public IEnumerable<AuthenticationProvider> Providers { get; set; } = new List<AuthenticationProvider>();
        public string ReturnUrl { get; set; }
    }
}
