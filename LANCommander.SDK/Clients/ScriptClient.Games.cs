using LANCommander.SDK.Extensions;
using LANCommander.SDK.Helpers;
using LANCommander.SDK.Models;
using LANCommander.SDK.PowerShell;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Services;

public partial class ScriptClient
{
    public async Task<int> Game_RunInstallScriptAsync(string installDirectory, Guid gameId)
    {
        int result = default;

        try
        {
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Install);

            using (var op = logger.BeginOperation("Executing install script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.Install);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());

                    if (manifest.CustomFields != null && manifest.CustomFields.Any())
                    {
                        foreach (var customField in manifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    script.UseFile(path);

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No install script found for game");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run an Install script");
        }

        return result;
    }

    public async Task<int> Game_RunUninstallScriptAsync(string installDirectory, Guid gameId)
    {
        int result = default;

        try
        {
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.Uninstall);

            using (var op = logger.BeginOperation("Executing uninstall script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.Uninstall);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    
                    if (manifest.CustomFields != null && manifest.CustomFields.Any())
                    {
                        foreach (var customField in manifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    script.UseFile(path);

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No uninstall script found");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to get an Uninstall script");
        }

        return result;
    }

    public async Task<int> Game_RunBeforeStartScriptAsync(string installDirectory, Guid gameId)
    {
        int result = default;

        try
        {
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.BeforeStart);

            using (var op = logger.BeginOperation("Executing before start script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.BeforeStart);
                    var playerAlias = await GameClient.GetPlayerAliasAsync(installDirectory, gameId);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("PlayerAlias", playerAlias);
                    
                    if (manifest.CustomFields != null && manifest.CustomFields.Any())
                    {
                        foreach (var customField in manifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    script.UseFile(path);

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("PlayerAlias", playerAlias)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No before start script found");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Before Start script");
        }

        return result;
    }

    public async Task<int> Game_RunAfterStopScriptAsync(string installDirectory, Guid gameId)
    {
        int result = default;

        try
        {
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.AfterStop);

            using (var op = logger.BeginOperation("Executing after stop script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.AfterStop);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("PlayerAlias", GameClient.GetPlayerAlias(installDirectory, gameId));
                    
                    if (manifest.CustomFields != null && manifest.CustomFields.Any())
                    {
                        foreach (var customField in manifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    script.UseFile(path);

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No after stop script found");
                }

                op.Complete();
            }

        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run an After Stop script");
        }

        return result;
    }

    public async Task<int> Game_RunNameChangeScriptAsync(string installDirectory, Guid gameId, string newName)
    {
        int result = default;

        try
        {
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.NameChange);
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);

            using (var op = logger.BeginOperation("Executing name change script"))
            {
                if (File.Exists(path))
                {
                    var oldName = await GameClient.GetPlayerAliasAsync(installDirectory, gameId);

                    if (oldName == newName)
                        oldName = string.Empty;

                    if (!string.IsNullOrWhiteSpace(oldName))
                        logger?.LogTrace("Old Name: {OldName}", oldName);

                    logger?.LogTrace("New Name: {NewName}", newName);

                    var script = powerShellScriptFactory.Create(Enums.ScriptType.NameChange);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("OldPlayerAlias", oldName);
                    script.AddVariable("NewPlayerAlias", newName);
                    
                    if (manifest.CustomFields != null && manifest.CustomFields.Any())
                    {
                        foreach (var customField in manifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("OldPlayerAlias", oldName)
                          .Enrich("NewPlayerAlias", newName)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    script.UseFile(path);

                    GameClient.UpdatePlayerAlias(installDirectory, gameId, newName);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No name change script found");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Name Change script");
        }

        return result;
    }

    public async Task<int> Game_RunKeyChangeScriptAsync(string installDirectory, Guid gameId, string key)
    {
        int result = default;

        try
        {
            var path = ScriptHelper.GetScriptFilePath(installDirectory, gameId, Enums.ScriptType.KeyChange);
            var manifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);

            using (var op = logger.BeginOperation("Executing key change script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.KeyChange);

                    logger?.LogTrace("New key is {Key}", key);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", manifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("AllocatedKey", key);
                    
                    if (manifest.CustomFields != null && manifest.CustomFields.Any())
                    {
                        foreach (var customField in manifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    script.UseFile(path);

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("ManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("ScriptPath", path)
                          .Enrich("AllocatedKey", key)
                          .Enrich("GameTitle", manifest.Title)
                          .Enrich("GameId", manifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    await GameClient.UpdateCurrentKeyAsync(installDirectory, gameId, key);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();
                }
                else
                {
                    logger?.LogTrace("No key change script found");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Key Change script");
        }

        return result;
    }

    public async Task<Package> Game_RunPackageScriptAsync(Script packageScript, Game game)
    {
        try
        {
            using (var op = logger.BeginOperation("Executing game package script"))
            {
                var script = powerShellScriptFactory.Create(Enums.ScriptType.Package);

                script.AddVariable("Game", game);

                script.UseInline(packageScript.Contents);
                
                try
                {
                    op
                        .Enrich("GameId", game.Id)
                        .Enrich("GameTitle", game.Title)
                        .Enrich("ScriptId", packageScript.Id)
                        .Enrich("ScriptName", packageScript.Name);
                }
                catch (Exception ex)
                {
                    logger?.LogError(ex, "Could not enrich logs");
                }
                
                if (Debug)
                    script.EnableDebug();

                return await script.ExecuteAsync<Package>();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Could not execute game package script");
        }

        return null;
    }
}