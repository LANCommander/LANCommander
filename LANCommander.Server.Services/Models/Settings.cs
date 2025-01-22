﻿using LANCommander.Server.Data.Enums;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;

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

    public class Settings
    {
        public int Port { get; set; } = 1337;
        public DatabaseProvider DatabaseProvider { get; set; } = DatabaseProvider.Unknown;
        public string DatabaseConnectionString { get; set; } = "";
        public string IGDBClientId { get; set; } = "";
        public string IGDBClientSecret { get; set; } = "";
        public LANCommanderTheme Theme { get; set; } = LANCommanderTheme.Dark;

        public BeaconSettings Beacon { get; set; } = new BeaconSettings();
        public AuthenticationSettings Authentication { get; set; } = new AuthenticationSettings();
        public RoleSettings Roles { get; set; } = new RoleSettings();
        public UserSaveSettings UserSaves { get; set; } = new UserSaveSettings();
        public ArchiveSettings Archives { get; set; } = new ArchiveSettings();
        public MediaSettings Media { get; set; } = new MediaSettings();
        public IPXRelaySettings IPXRelay { get; set; } = new IPXRelaySettings();
        public ServerSettings Servers { get; set; } = new ServerSettings();
        public UpdateSettings Update { get; set; } = new UpdateSettings();
        public LauncherSettings Launcher { get; set; } = new LauncherSettings();
        public LogSettings Logs { get; set; } = new LogSettings();

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
        public SameSiteMode MinimumSameSitePolicy { get; set; } = SameSiteMode.Strict;
        public CookieSecurePolicy CookieSecurePolicy { get; set; } = CookieSecurePolicy.Always;
        public IEnumerable<AuthenticationProvider> AuthenticationProviders { get; set; } = new List<AuthenticationProvider>();
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
        public string Authority { get; set; }
        public string AuthorizationEndpoint { get; set; }
        public string TokenEndpoint { get; set; }
        public string UserInfoEndpoint { get; set; }
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
        public string StoragePath { get; set; } = "Saves";
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
        public long MaxSize { get; set; } = 25;
    }

    public class IPXRelaySettings
    {
        public bool Enabled { get; set; } = false;
        public string Host { get; set; } = "";
        public int Port { get; set; } = 213;
        public bool Logging { get; set; } = false;
    }

    public class ServerSettings
    {
        public string StoragePath { get; set; } = "Servers";
    }

    public class UpdateSettings
    {
        public string StoragePath { get; set; } = "Updates";
    }

    public class LauncherSettings
    {
        public string StoragePath { get; set; } = "Launcher";
        public bool HostUpdates { get; set; } = true;
    }

    public class LogSettings
    {
        public string StoragePath { get; set; } = "Logs";
        public LogInterval ArchiveEvery { get; set; } = LogInterval.Day;
        public int MaxArchiveFiles { get; set; } = 10;
    }
}
