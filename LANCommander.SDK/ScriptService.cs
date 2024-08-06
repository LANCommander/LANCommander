using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LANCommander.SDK
{
    public class ScriptService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public ScriptService(Client client)
        {
            Client = client;
        }

        public ScriptService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<int> RunInstallScriptAsync(string installDirectory, Guid gameId, bool debug = false)
        {
            try
            {
                var manifest = ManifestHelper.Read(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Install);

                if (File.Exists(path))
                {
                    Logger?.LogTrace("Running install script for game {GameTitle} ({gameId})", manifest.Title, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", Client.BaseUrl.ToString());

                    script.UseFile(ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Install));

                    if (debug)
                        script.EnableDebug();

                    return await script.ExecuteAsync();
                }

                Logger?.LogTrace("No install script found for game {GameTitle} ({gameId})", manifest.Title, gameId);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run an Install script");
            }

            return 0;
        }

        public async Task<int> RunUninstallScriptAsync(string installDirectory, Guid gameId, bool debug = false)
        {
            try
            {
                var manifest = ManifestHelper.Read(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Uninstall);

                if (File.Exists(path))
                {
                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                    script.UseFile(path);

                    if (debug)
                        script.EnableDebug();

                    return await script.ExecuteAsync();
                }

                Logger?.LogTrace("No uninstall script found for game {GameTitle} ({gameId})", manifest.Title, gameId);
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run an Uninstall script");
            }

            return 0;
        }

        public async Task RunBeforeStartScriptAsync(string installDirectory, Guid gameId, bool debug = false)
        {

            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.BeforeStart);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(installDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                    script.AddVariable("PlayerAlias", GameService.GetPlayerAlias(installDirectory, gameId));

                    script.UseFile(path);

                    if (debug)
                        script.EnableDebug();

                    await script.ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Before Start script");
            }
        }

        public async Task RunAfterStopScriptAsync(string installDirectory, Guid gameId, bool debug = false)
        {
            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.AfterStop);

                if (File.Exists(path))
                {
                    var manifest = ManifestHelper.Read(installDirectory, gameId);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                    script.AddVariable("PlayerAlias", GameService.GetPlayerAlias(installDirectory, gameId));

                    script.UseFile(path);

                    if (debug)
                        script.EnableDebug();

                    await script.ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run an After Stop script");
            }
        }

        public async Task RunNameChangeScriptAsync(string installDirectory, Guid gameId, string newName, bool debug = false)
        {
            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.NameChange);
                var manifest = ManifestHelper.Read(installDirectory, gameId);

                if (File.Exists(path))
                {
                    Logger?.LogTrace("Running name change script for game {GameTitle} ({gameId})", manifest.Title, gameId);

                    var oldName = GameService.GetPlayerAlias(installDirectory, gameId);

                    if (!String.IsNullOrWhiteSpace(oldName))
                        Logger?.LogTrace("Old Name: {OldName}", oldName);

                    Logger?.LogTrace("New Name: {NewName}", newName);

                    var script = new PowerShellScript();

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                    script.AddVariable("OldPlayerAlias", oldName);
                    script.AddVariable("NewPlayerAlias", newName);

                    script.UseFile(path);

                    SDK.GameService.UpdatePlayerAlias(installDirectory, gameId, newName);

                    if (debug)
                        script.EnableDebug();

                    await script.ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }
        }

        public async Task RunKeyChangeScriptAsync(string installDirectory, Guid gameId, string key, bool debug = false)
        {
            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.NameChange);
                var manifest = ManifestHelper.Read(installDirectory, gameId);

                if (File.Exists(path))
                {
                    Logger?.LogTrace("Running key change script for game {GameTitle} ({gameId})", manifest.Title, gameId);

                    var script = new PowerShellScript();

                    Logger?.LogTrace("New key is {Key}", key);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                    script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                    script.AddVariable("AllocatedKey", key);

                    script.UseFile(path);

                    GameService.UpdateCurrentKey(installDirectory, gameId, key);

                    if (debug)
                        script.EnableDebug();

                    await script.ExecuteAsync();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Key Change script");
            }
        }
    }
}
