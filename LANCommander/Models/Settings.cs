namespace LANCommander.Models
{
    public class LANCommanderSettings
    {
        public int Port { get; set; } = 1337;
        public bool Beacon { get; set; } = true;
        public string DatabaseConnectionString { get; set; } = "";
        public string IGDBClientId { get; set; } = "";
        public string IGDBClientSecret { get; set; } = "";

        public LANCommanderAuthenticationSettings Authentication { get; set; } = new LANCommanderAuthenticationSettings();
    }

    public class LANCommanderAuthenticationSettings
    {
        public string TokenSecret { get; set; } = "";
        public int TokenLifetime { get; set; } = 30;
        public bool PasswordRequireNonAlphanumeric { get; set; }
        public bool PasswordRequireLowercase { get; set; }
        public bool PasswordRequireUppercase { get; set; }
        public bool PasswordRequireDigit { get; set; }
        public int PasswordRequiredLength { get; set; } = 8;
    }
}
