using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace LANCommander.SDK
{
    public class ScriptService
    {
        private readonly ILogger Logger;

        private readonly Client Client;

        public delegate Task<bool> ExternalScriptRunnerHandler(PowerShellScript script);
        public event ExternalScriptRunnerHandler ExternalScriptRunner;

        public bool Debug { get; set; } = false;

        public Func<System.Management.Automation.PowerShell, Task> OnDebugStart;
        public Func<System.Management.Automation.PowerShell, Task> OnDebugBreak;
        public Func<LogLevel, string, Task> OnOutput;

        public ScriptService(Client client)
        {
            Client = client;
        }

        public ScriptService(Client client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }

        public async Task<int> RunInstallScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = ManifestHelper.Read(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Install);

                using (var op = Logger.BeginOperation("Executing install script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.Install);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                        script.UseFile(path);

                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);

                        if (Debug)
                        {
                            script.EnableDebug();
                            script.OnDebugBreak = OnDebugBreak;
                            script.OnOutput = OnOutput;
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        Logger?.LogTrace("No install script found for game");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run an Install script");
            }

            return result;
        }

        public async Task<int> RunUninstallScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = ManifestHelper.Read(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Uninstall);

                using (var op = Logger.BeginOperation("Executing uninstall script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.Uninstall);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                        script.UseFile(path);

                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);

                        if (Debug)
                        {
                            script.EnableDebug();
                            script.OnDebugBreak = OnDebugBreak;
                            script.OnOutput = OnOutput;
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        Logger?.LogTrace("No uninstall script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to get an Uninstall script");
            }

            return result;
        }

        public async Task<int> RunBeforeStartScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = ManifestHelper.Read(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.BeforeStart);

                using (var op = Logger.BeginOperation("Executing before start script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.BeforeStart);
                        var playerAlias = GameService.GetPlayerAlias(installDirectory, gameId);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                        script.AddVariable("PlayerAlias", playerAlias);
                        script.UseFile(path);

                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("PlayerAlias", playerAlias)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);

                        if (Debug)
                        {
                            script.EnableDebug();
                            script.OnDebugBreak = OnDebugBreak;
                            script.OnOutput = OnOutput;
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        Logger?.LogTrace("No before start script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Before Start script");
            }

            return result;
        }

        public async Task<int> RunAfterStopScriptAsync(string installDirectory, Guid gameId)
        {
            int result = default;

            try
            {
                var manifest = ManifestHelper.Read(installDirectory, gameId);
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.AfterStop);

                using (var op = Logger.BeginOperation("Executing after stop script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.AfterStop);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                        script.AddVariable("PlayerAlias", GameService.GetPlayerAlias(installDirectory, gameId));
                        script.UseFile(path);

                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);

                        if (Debug)
                        {
                            script.EnableDebug();
                            script.OnDebugBreak = OnDebugBreak;
                            script.OnOutput = OnOutput;
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        Logger?.LogTrace("No after stop script found");
                    }
                }

            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run an After Stop script");
            }

            return result;
        }

        public async Task<int> RunNameChangeScriptAsync(string installDirectory, Guid gameId, string newName)
        {
            int result = default;

            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.NameChange);
                var manifest = ManifestHelper.Read(installDirectory, gameId);

                using (var op = Logger.BeginOperation("Executing name change script"))
                {
                    if (File.Exists(path))
                    {
                        var oldName = GameService.GetPlayerAlias(installDirectory, gameId);

                        if (oldName == newName)
                            oldName = String.Empty;

                        if (!String.IsNullOrWhiteSpace(oldName))
                            Logger?.LogTrace("Old Name: {OldName}", oldName);

                        Logger?.LogTrace("New Name: {NewName}", newName);

                        var script = new PowerShellScript(Enums.ScriptType.NameChange);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                        script.AddVariable("OldPlayerAlias", oldName);
                        script.AddVariable("NewPlayerAlias", newName);

                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("OldPlayerAlias", oldName)
                          .Enrich("NewPlayerAlias", newName)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);

                        script.UseFile(path);

                        SDK.GameService.UpdatePlayerAlias(installDirectory, gameId, newName);

                        if (Debug)
                        {
                            script.EnableDebug();
                            script.OnDebugBreak = OnDebugBreak;
                            script.OnOutput = OnOutput;
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        Logger?.LogTrace("No name change script found");
                    }

                    op.Complete();
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Name Change script");
            }

            return result;
        }

        public async Task<int> RunKeyChangeScriptAsync(string installDirectory, Guid gameId, string key)
        {
            int result = default;

            try
            {
                var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, SDK.Enums.ScriptType.KeyChange);
                var manifest = ManifestHelper.Read(installDirectory, gameId);

                using (var op = Logger.BeginOperation("Executing key change script"))
                {
                    if (File.Exists(path))
                    {
                        var script = new PowerShellScript(Enums.ScriptType.KeyChange);

                        if (Debug)
                            script.OnDebugStart = OnDebugStart;

                        Logger?.LogTrace("New key is {Key}", key);

                        script.AddVariable("InstallDirectory", installDirectory);
                        script.AddVariable("GameManifest", manifest);
                        script.AddVariable("DefaultInstallDirectory", Client.DefaultInstallDirectory);
                        script.AddVariable("ServerAddress", Client.BaseUrl.ToString());
                        script.AddVariable("AllocatedKey", key);
                        script.UseFile(path);

                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("AllocatedKey", key)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);

                        GameService.UpdateCurrentKey(installDirectory, gameId, key);

                        if (Debug)
                        {
                            script.EnableDebug();
                            script.OnDebugBreak = OnDebugBreak;
                            script.OnOutput = OnOutput;
                        }

                        bool handled = false;

                        if (ExternalScriptRunner != null)
                            handled = await ExternalScriptRunner.Invoke(script);

                        if (!handled)
                            result = await script.ExecuteAsync<int>();
                    }
                    else
                    {
                        Logger?.LogTrace("No key change script found");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Key Change script");
            }

            return result;
        }
    }
}
