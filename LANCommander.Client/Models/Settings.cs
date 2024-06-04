using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Client.Models
{
    public class Settings
    {
        public DatabaseSettings Database { get; set; } = new DatabaseSettings();
        public AuthenticationSettings Authentication { get; set; } = new AuthenticationSettings();
        public GameSettings Games { get; set; } = new GameSettings();
        public MediaSettings Media { get; set; } = new MediaSettings();
        public ProfileSettings Profile { get; set; } = new ProfileSettings();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=LANCommander.db;Cache=Shared";
    }

    public class AuthenticationSettings
    {
        public string ServerAddress { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
    }

    public class GameSettings
    {
        public string DefaultInstallDirectory { get; set; } = "C:\\Games";
    }

    public class MediaSettings
    {
        public string StoragePath { get; set; } = "Media";
    }

    public class ProfileSettings
    {
        public Guid Id { get; set; }
        public string Alias { get; set; }
        public string Avatar { get; set; }
    }
}
