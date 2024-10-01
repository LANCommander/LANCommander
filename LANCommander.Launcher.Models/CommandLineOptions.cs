using CommandLine;
using LANCommander.SDK.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Models
{
    public enum ArchiveType
    {
        Game,
        Redistributable,
        Server
    }

    [Verb("RunScript", HelpText = "Run a script for a game")]
    public class RunScriptCommandLineOptions
    {
        [Option("GameId", HelpText = "The GUID of the installed game")]
        public Guid GameId { get; set; }

        [Option("InstallDirectory", HelpText = "The base directory in which the installed game is located")]
        public string InstallDirectory { get; set; }

        [Option("Type", HelpText = "The type of script to run")]
        public ScriptType Type { get; set; }

        [Option("OldPlayerAlias", HelpText = "The old name to use in a name change script")]
        public string OldPlayerAlias { get; set; }

        [Option("NewPlayerAlias", HelpText = "The new name to use in a name change script")]
        public string NewPlayerAlias { get; set; }

        [Option("AllocatedKey", HelpText = "The new key to use in a key change script")]
        public string AllocatedKey { get; set; }
    }

    [Verb("Install", HelpText = "Install a game from the server")]
    public class InstallCommandLineOptions
    {
        [Option("GameId", HelpText = "The GUID of the game to install")]
        public Guid GameId { get; set; }

        [Option("InstallDirectory", HelpText = "The directory the game folder will be extracted to")]
        public string InstallDirectory { get; set; } = "";
    }

    [Verb("Uninstall", HelpText = "Uninstall a game from the local device")]
    public class UninstallCommandLineOptions
    {
        [Option("GameId", HelpText = "The GUID of the game to uninstall")]
        public Guid GameId { get; set; }
    }

    [Verb("Run", HelpText = "Start a game")]
    public class RunCommandLineOptions
    {
        [Option("GameId", HelpText = "The GUID of the game to run")]
        public Guid GameId { get; set; }

        [Option("ActionId", HelpText = "The GUID of the action to run")]
        public Guid ActionId { get; set; }
    }

    [Verb("Sync", HelpText = "Sync library items from the server")]
    public class SyncCommandLineOptions { }

    [Verb("Import", HelpText = "Upload and import a game/redistributable/server to the server (Admin Only)")]
    public class ImportCommandLineOptions
    {
        [Option("Path", HelpText = "Path to the import file", Required = true)]
        public string Path { get; set; }

        [Option("Type", HelpText = "The type of import file", Required = true)]
        public ArchiveType Type { get; set; }
    }

    [Verb("Export", HelpText = "Export a game/redistributable/server from the server (Admin Only)")]
    public class ExportCommandLineOptions
    {
        [Option("Path", HelpText = "The destination path for the LCX export file", Required = true)]
        public string Path { get; set; }

        [Option("Id", HelpText = "The ID of the entity to export", Required = true)]
        public Guid Id { get; set; }

        [Option("Type", HelpText = "The type of archive to export", Required = true)]
        public ImportArchiveType Type { get; set; }
    }

    [Verb("Login", HelpText = "Login to a LANCommander server")]
    public class LoginCommandLineOptions
    {
        [Option("Username", Required = true)]
        public string Username { get; set; }

        [Option("Password", Required = true)]
        public string Password { get; set; }

        [Option("ServerAddress")]
        public string ServerAddress { get; set; }
    }

    [Verb("Logout", HelpText = "Logout and clear local credentials")]
    public class LogoutCommandLineOptions { }

    [Verb("ChangeAlias", HelpText = "Change the current logged in user's alias")]
    public class ChangeAliasCommandLineOptions
    {
        [Option("Alias", Required = true)]
        public string Alias { get; set; }
    }
}
