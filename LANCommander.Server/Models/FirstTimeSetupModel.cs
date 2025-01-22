using LANCommander.Server.Data.Enums;

namespace LANCommander.Server.Models
{
    public class FirstTimeSetupModel
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string PasswordConfirm { get; set; }
    }
}
