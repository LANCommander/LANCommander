﻿using LANCommander.Launcher.Enums;
using Microsoft.Extensions.Logging;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public class Settings
    {
        public int LaunchCount { get; set; } = 0;

        public DatabaseSettings Database { get; set; } = new DatabaseSettings();
        public AuthenticationSettings Authentication { get; set; } = new AuthenticationSettings();
        public GameSettings Games { get; set; } = new GameSettings();
        public MediaSettings Media { get; set; } = new MediaSettings();
        public ProfileSettings Profile { get; set; } = new ProfileSettings();
        public FilterSettings Filter { get; set; } = new FilterSettings();
        public DebugSettings Debug { get; set; } = new DebugSettings();
        public UpdateSettings Updates { get; set; } = new UpdateSettings();
    }

    public class DatabaseSettings
    {
        public string ConnectionString { get; set; } = "Data Source=LANCommander.db;Cache=Shared";
        public string BackupsPath { get; set; } = "Backups";
    }

    public class AuthenticationSettings
    {
        public string ServerAddress { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public bool OfflineMode { get; set; } = false;
    }

    public class GameSettings
    {
        public string DefaultInstallDirectory { get; set; } = "C:\\Games";
        public Guid? LastSelected { get; set; } = null;
    }

    public class MediaSettings
    {
        public string StoragePath { get; set; } = "Media";
    }

    public class ProfileSettings
    {
        public Guid Id { get; set; }
        public string Alias { get; set; }
        public Guid AvatarId { get; set; }
    }

    public class FilterSettings
    {
        public string? Title { get; set; }
        public GroupBy GroupBy { get; set; } = GroupBy.Collection;
        public IEnumerable<string> Engines { get; set; }
        public IEnumerable<string> Genres { get; set; }
        public IEnumerable<string> Tags { get; set; }
        public IEnumerable<string> Platforms { get; set; }
        public IEnumerable<string> Developers { get; set; }
        public IEnumerable<string> Publishers { get; set; }
        public int? MinPlayers { get; set; }
        public int? MaxPlayers { get; set; }
        public bool Installed { get; set; } = false;
    }

    public class DebugSettings
    {
        public bool EnableScriptDebugging { get; set; } = false;
        public LogLevel LoggingLevel { get; set; } = LogLevel.Warning;
        public string LoggingPath { get; set; } = "Logs";
        public FileArchivePeriod LoggingArchivePeriod { get; set; } = FileArchivePeriod.Day;
        public int MaxArchiveFiles { get; set; } = 10;
    }

    public class UpdateSettings
    {
        public string StoragePath { get; set; } = "Updates";
    }
}
