namespace LANCommander.Models
{
    public enum LANCommanderTheme
    {
        Light,
        Dark
    }

    public class LANCommanderSettings
    {
        public int Port { get; set; } = 1337;
        public string DatabaseConnectionString { get; set; } = "Data Source=LANCommander.db;Cache=Shared";
        public string IGDBClientId { get; set; } = "";
        public string IGDBClientSecret { get; set; } = "";
        public LANCommanderTheme Theme { get; set; } = LANCommanderTheme.Light;

        public LANCommanderBeaconSettings Beacon { get; set; } = new LANCommanderBeaconSettings();
        public LANCommanderAuthenticationSettings Authentication { get; set; } = new LANCommanderAuthenticationSettings();
        public LANCommanderRoleSettings Roles { get; set; } = new LANCommanderRoleSettings();
        public LANCommanderUserSaveSettings UserSaves { get; set; } = new LANCommanderUserSaveSettings();
        public LANCommanderArchiveSettings Archives { get; set; } = new LANCommanderArchiveSettings();
        public LANCommanderMediaSettings Media { get; set; } = new LANCommanderMediaSettings();
        public LANCommanderIPXRelaySettings IPXRelay { get; set; } = new LANCommanderIPXRelaySettings();
    }

    public class LANCommanderBeaconSettings
    {
        public bool Enabled { get; set; } = true;
        public string Address { get; set; } = "";
    }

    public class LANCommanderAuthenticationSettings
    {
        public bool RequireApproval { get; set; } = false;
        public string TokenSecret { get; set; } = Guid.NewGuid().ToString();
        public int TokenLifetime { get; set; } = 30;
        public bool PasswordRequireNonAlphanumeric { get; set; } = false;
        public bool PasswordRequireLowercase { get; set; } = false;
        public bool PasswordRequireUppercase { get; set; } = false;
        public bool PasswordRequireDigit { get; set; } = true;
        public int PasswordRequiredLength { get; set; } = 8;
    }

    public class LANCommanderRoleSettings
    {
        public Guid DefaultRoleId { get; set; }
        public bool RestrictGamesByCollection { get; set; } = false;
    }

    public class LANCommanderUserSaveSettings
    {
        public string StoragePath { get; set; } = "Saves";
        public int MaxSize { get; set; } = 25;
    }

    public class LANCommanderArchiveSettings
    {
        public bool EnablePatching { get; set; } = false;
        public string StoragePath { get; set; } = "Uploads";
    }

    public class LANCommanderMediaSettings
    {
        public string SteamGridDbApiKey { get; set; } = "";
        public string StoragePath { get; set; } = "Media";
        public long MaxSize { get; set; } = 25;
    }

    public class LANCommanderIPXRelaySettings
    {
        public bool Enabled { get; set; } = false;
        public int Port { get; set; } = 213;
        public bool Logging { get; set; } = false;
    }
}
