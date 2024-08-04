using LANCommander.Launcher.Data.Models;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.PowerShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services
{
    public class ScriptService : BaseService
    {
        private readonly SDK.Client Client;
        public ScriptService(SDK.Client client)
        {
            Client = client;
        }

        internal int RunInstallScript(Game game, Guid gameId)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.Install);

                if (File.Exists(path))
                {
                    Logger?.Trace("Running install script for game {GameTitle} ({GameId})", game.Title, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);

                    script.UseFile(ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.Install));

                    if (settings.Debug.EnableScriptDebugging)
                        script.EnableDebug();

                    return script.Execute();
                }

                Logger?.Trace("No install script found for game {GameTitle} ({GameId})", game.Title, gameId);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run an Install script");
            }

            return 0;
        }

        internal int RunUninstallScript(Game game, Guid gameId)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.Uninstall);

                if (File.Exists(path))
                {
                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("PlayerAlias", settings.Profile.Alias);

                    script.UseFile(path);

                    if (settings.Debug.EnableScriptDebugging)
                        script.EnableDebug();

                    return script.Execute();
                }

                Logger?.Trace("No uninstall script found for game {GameTitle} ({GameId})", game.Title, gameId);
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run an Uninstall script");
            }            

            return 0;
        }

        internal void RunBeforeStartScript(Game game, Guid gameId)
        {

            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.BeforeStart);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("PlayerAlias", settings.Profile.Alias);

                    script.UseFile(path);

                    if (settings.Debug.EnableScriptDebugging)
                        script.EnableDebug();

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run a Before Start script");
            }
        }

        internal void RunAfterStopScript(Game game, Guid gameId)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.AfterStop);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("PlayerAlias", settings.Profile.Alias);

                    script.UseFile(path);

                    if (settings.Debug.EnableScriptDebugging)
                        script.EnableDebug();

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run an After Stop script");
            }
        }

        internal void RunNameChangeScript(Game game, Guid gameId)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.NameChange);

                var oldName = SDK.GameService.GetPlayerAlias(game.InstallDirectory, gameId);
                var newName = settings.Profile.Alias;

                if (File.Exists(path))
                {
                    Logger?.Trace("Running name change script for game {GameTitle} ({GameId})", game.Title, game.Id);

                    if (!String.IsNullOrWhiteSpace(oldName))
                        Logger?.Trace("Old Name: {OldName}", oldName);

                    Logger?.Trace("New Name: {NewName}", newName);

                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("OldPlayerAlias", oldName);
                    script.AddVariable("NewPlayerAlias", newName);

                    script.UseFile(path);

                    SDK.GameService.UpdatePlayerAlias(game.InstallDirectory, gameId, newName);

                    if (settings.Debug.EnableScriptDebugging)
                        script.EnableDebug();

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }
        }

        internal void RunKeyChangeScript(Game game, Guid gameId)
        {
            try
            {
                var settings = SettingService.GetSettings();
                var path = ScriptHelper.GetScriptFilePath(game.InstallDirectory, gameId, SDK.Enums.ScriptType.NameChange);

                if (File.Exists(path))
                {
                    Logger?.Trace("Running key change script for game {GameTitle} ({GameId})", game.Title, game.Id);

                    var manifest = ManifestHelper.Read(game.InstallDirectory, gameId);

                    var script = new PowerShellScript();

                    var key = Client.Games.GetAllocatedKey(manifest.Id);

                    Logger?.Trace("New key is {Key}", key);

                    script.AddVariable("InstallDirectory", game.InstallDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settings.Games.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", settings.Authentication.ServerAddress);
                    script.AddVariable("AllocatedKey", key);

                    script.UseFile(path);

                    SDK.GameService.UpdateCurrentKey(game.InstallDirectory, gameId, key);

                    if (settings.Debug.EnableScriptDebugging)
                        script.EnableDebug();

                    script.Execute();
                }
            }
            catch (Exception ex)
            {
                Logger?.Error(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }
        }
    }
}
