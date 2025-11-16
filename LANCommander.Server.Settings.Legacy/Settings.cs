using LANCommander.Server.Data.Enums;
using Humanizer.Bytes;
using LANCommander.SDK.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.Models
{
    public enum LANCommanderTheme
    {
        Light,
        Dark
    }

    public enum LogInterval
    {
        Infinite,
        Year,
        Month,
        Day,
        Hour,
        Minute
    }

    public enum AuthenticationProviderType
    {
        OAuth2,
        OpenIdConnect,
        Saml
    }

    public enum LoggingProviderType
    {
        File,
        Console,
        SignalR,
        Seq,
        ElasticSearch,
    }

    public enum ReleaseChannel
    {
        Stable,
        Prerelease,
        Nightly,
    }
    
    public enum LauncherPlatform
    {
        Windows,
        Linux,
        macOS,
    }

    public enum LauncherArchitecture
    {
        x64,
        arm64,
    }

    public class Settings
    {
        public int Port { get; set; } = 1337;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Unknown;
        public string DatabaseConnectionString { get; set; } = "";
        public string IGDBClientId { get; set; } = "";
        public string IGDBClientSecret { get; set; } = "";
        public LANCommanderTheme Theme { get; set; } = LANCommanderTheme.Dark;
        public bool UseSSL { get; set; } = false;
        public string CertificatePath { get; set; } = "";
        public string CertificatePassword { get; set; } = "";
        public int SSLPort { get; set; } = 31337;
        
        public BeaconSettings Beacon { get; set; } = new BeaconSettings();
        public AuthenticationSettings Authentication { get; set; } = new AuthenticationSettings();
        public RoleSettings Roles { get; set; } = new RoleSettings();
        public UserSaveSettings UserSaves { get; set; } = new UserSaveSettings();
        public ArchiveSettings Archives { get; set; } = new ArchiveSettings();
        public MediaSettings Media { get; set; } = new MediaSettings();
        public IPXRelaySettings IPXRelay { get; set; } = new IPXRelaySettings();
        public ServerSettings Servers { get; set; } = new ServerSettings();
        public BackupSettings Backups { get; set; } = new BackupSettings();
        public UpdateSettings Update { get; set; } = new UpdateSettings();
        public LauncherSettings Launcher { get; set; } = new LauncherSettings();
        public LogSettings Logs { get; set; } = new LogSettings();
        public LibrarySettings Library { get; set; } = new LibrarySettings();
        public ScriptSettings Scripts { get; set; } = new ScriptSettings();
        public SteamCmdSettings SteamCMD { get; set; } = new SteamCmdSettings();

        private DriveInfo[] Drives { get; set; } = DriveInfo.GetDrives();
        public DriveInfo[] GetDrives()
        {
            if (Drives == null || Drives.Length == 0)
                Drives = DriveInfo.GetDrives();

            return Drives;
        }
    }

    public class BeaconSettings
    {
        public bool Enabled { get; set; } = true;
        public string Name { get; set; } = "LANCommander";
        public string Address { get; set; } = "";
        public int Port { get; set; } = 35891;
    }

    public class AuthenticationSettings
    {
        public bool RequireApproval { get; set; } = false;
        public string TokenSecret { get; set; } = Guid.NewGuid().ToString();
        public int TokenLifetime { get; set; } = 30;
        public bool PasswordRequireNonAlphanumeric { get; set; } = false;
        public bool PasswordRequireLowercase { get; set; } = false;
        public bool PasswordRequireUppercase { get; set; } = false;
        public bool PasswordRequireDigit { get; set; } = true;
        public int PasswordRequiredLength { get; set; } = 8;

        public CookiePolicy HttpCookiePolicy { get; set; } = new()
        {
            SameSite = SameSiteMode.Lax,
            Secure = CookieSecurePolicy.None
        };

        public CookiePolicy HttpsCookiePolicy { get; set; } = new()
        {
            SameSite = SameSiteMode.None,
            Secure = CookieSecurePolicy.SameAsRequest
        };
        
        public IEnumerable<AuthenticationProvider> AuthenticationProviders { get; set; } = new List<AuthenticationProvider>();
    }

    public class CookiePolicy
    {
        public SameSiteMode SameSite { get; set; } = SameSiteMode.None;
        public CookieSecurePolicy Secure { get; set; } = CookieSecurePolicy.SameAsRequest;
    }

    public class AuthenticationProvider
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public AuthenticationProviderType Type { get; set; } = AuthenticationProviderType.OAuth2;
        public string Color { get; set; }
        public string Icon { get; set; }
        public string Documentation { get; set; }
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AuthorizationEndpoint { get; set; }
        public string TokenEndpoint { get; set; }
        public string UserInfoEndpoint { get; set; }
        public string ConfigurationUrl { get; set; }
        public IEnumerable<string> Scopes { get; set; } = new List<string>();
        public IEnumerable<ClaimMapping> ClaimMappings { get; set; } = new List<ClaimMapping>();
        
        public string GetCustomFieldName()
        {
            return $"ExternalId/{Slug}";
        }
    }

    public class ClaimMapping
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    public class RoleSettings
    {
        public Guid DefaultRoleId { get; set; }
        public bool RestrictGamesByCollection { get; set; } = false;
    }

    public class UserSaveSettings
    {
        public int MaxSize { get; set; } = 25;
        public int MaxSaves { get; set; } = 0;
    }

    public class ArchiveSettings
    {
        public bool EnablePatching { get; set; } = false;
        public bool AllowInsecureDownloads { get; set; } = false;
        public int MaxChunkSize { get; set; } = 50;
    }

    public class MediaSettings
    {
        public string SteamGridDbApiKey { get; set; } = "";

        public IEnumerable<MediaTypeSettings> MediaTypes { get; set; } =
            new List<MediaTypeSettings>()
            {
                new MediaTypeSettings
                {
                    Type = MediaType.Icon,
                    MaxFileSize = 2 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(32, 32),
                        MaxSize = new ThumbnailSize(128, 128),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Cover,
                    MaxFileSize = 6 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(240, 360),
                        MaxSize = new ThumbnailSize(600, 900),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Background,
                    MaxFileSize = 8 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(690, 310),
                        MaxSize = new ThumbnailSize(3840, 1240),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Avatar,
                    MaxFileSize = 2 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(64, 64),
                        MaxSize = new ThumbnailSize(256, 256),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Logo,
                    MaxFileSize = 4 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(400, 400),
                        MaxSize = new ThumbnailSize(1000, 1000),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.Manual,
                    MaxFileSize = 4 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(240, 360),
                        MaxSize = new ThumbnailSize(600, 900),
                    }
                },
                new MediaTypeSettings
                {
                    Type = MediaType.PageImage,
                    MaxFileSize = 4 * ByteSize.BytesInMegabyte,
                    Thumbnails = new MediaTypeThumbnailSettings
                    {
                        MinSize = new ThumbnailSize(480, 480),
                        MaxSize = new ThumbnailSize(1920, 1920),
                    }
                },
            };

        public MediaTypeSettings? GetMediaTypeConfig(MediaType type) => MediaTypes.FirstOrDefault(x => x.Type == type);
    }

    public class MediaTypeSettings
    {
        public MediaType Type { get; set; }
        public long MaxFileSize { get; set; }
        public MediaTypeThumbnailSettings Thumbnails { get; set; } = new();
    }

    public class MediaTypeThumbnailSettings
    {
        public ThumbnailSize MinSize { get; set; }
        public ThumbnailSize MaxSize { get; set; }
        public int Scale { get; set; } = 50;
        public bool Enabled { get; set; } = true;
        public int Quality { get; set; } = 75;
    }

    public class ThumbnailSize
    {
        public int Width { get; set; }
        public int Height { get; set; }

        public ThumbnailSize()
        {
        }

        public ThumbnailSize(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    public class IPXRelaySettings
    {
        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = "";
        public int Port { get; set; } = 213;
        public bool Logging { get; set; } = false;
    }

    public class BackupSettings
    {
        public string StoragePath { get; set; } = "Backups";
    }

    public class ServerSettings
    {
        public string StoragePath { get; set; } = "Servers";
        public IEnumerable<ServerEngineConfiguration> ServerEngines { get; set; } = new List<ServerEngineConfiguration>()
        {
            new ServerEngineConfiguration
            {
                Name = "Local",
                Type = ServerEngine.Local,
            },
            new ServerEngineConfiguration
            {
                Name = "Docker",
                Type = ServerEngine.Docker,
                Address = "unix:///var/run/docker.sock",
            }
        };
    }

    public class ServerEngineConfiguration
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Local";
        public ServerEngine Type { get; set; } = ServerEngine.Local;
        public string Address { get; set; } = "";
    }

    public class UpdateSettings
    {
        public string StoragePath { get; set; } = "Updates";
        public ReleaseChannel ReleaseChannel { get; set; } = ReleaseChannel.Stable;
    }

    public class LauncherSettings
    {
        public string StoragePath { get; set; } = "Launcher";
        /// <summary>
        /// Whether to include locally downloaded launcher files and provide these for download
        /// </summary>
        public bool HostUpdates { get; set; } = true;
        /// <summary>
        /// Whether to include online launcher files to link to for download
        /// </summary>
        public bool IncludeOnlineUpdates { get; set; } = false;
        public string VersionOverride { get; set; } = "";
        public IEnumerable<LauncherArchitecture> Architectures { get; set; } = new[] { LauncherArchitecture.x64, LauncherArchitecture.arm64 };
        public IEnumerable<LauncherPlatform> Platforms { get; set; } = new[] { LauncherPlatform.Windows };
    }

    public class LoggingProvider
    {
        public string Name { get; set; }
        public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
        public bool Enabled { get; set; } = true;
        public LoggingProviderType Type { get; set; } = LoggingProviderType.Console;
        public LogInterval? ArchiveEvery { get; set; }
        public int MaxArchiveFiles { get; set; } = 10;
        public string ConnectionString { get; set; } = "";
    }

    public class LogSettings
    {
        public bool IgnorePings { get; set; } = true;
        public IEnumerable<LoggingProvider> Providers { get; set; } = [
            new()
            {
                Name = "Console",
                MinimumLevel = LogLevel.Information, Type = LoggingProviderType.Console
            },
            new()
            {
                Name = "File",
                MinimumLevel = LogLevel.Information,
                Type = LoggingProviderType.File,
                ConnectionString = "Logs"
            },
            new()
            {
                Name = "Server Console",
                MinimumLevel = LogLevel.Information,
                Type = LoggingProviderType.SignalR
            },
        ];
    }

    public class LibrarySettings
    {
        public bool EnableUserLibraries { get; set; } = true;
    }

    public class ScriptSettings
    {
        public bool EnableAutomaticRepackaging { get; set; } = false;
        public int RepackageEvery { get; set; } = 24;
    }

    public class SteamCmdSettings
    {
        public string Path { get; set; } = "";
        public string InstallDirectory { get; set; } = "";
        public ICollection<SteamCmdProfile> Profiles { get; set; } = new List<SteamCmdProfile>();
    }

    public class SteamCmdProfile
    {
        public string Username { get; set; }
        public string InstallDirectory { get; set; }
    }
}
