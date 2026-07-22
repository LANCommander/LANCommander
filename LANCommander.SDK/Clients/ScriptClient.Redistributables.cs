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
using System.Threading;
using System.Threading.Tasks;
using LANCommander.SDK.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace LANCommander.SDK.Services;

public partial class ScriptClient
{
    public async Task<bool> Redistributable_RunDetectInstallScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
    {
        bool result = default;
        
        var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
        var redistributableManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Redistributable>(installDirectory, redistributableId);
        
        var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.DetectInstall);

        if (File.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.DetectInstall))
        {
            logger?.LogTrace("Skipping detect install script; not supported on the current runtime");
            return result;
        }

        try
        {
            if (File.Exists(path))
            {
                using (var op = logger.BeginOperation("Executing install detection script"))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.DetectInstall);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("RedistributableName", redistributableManifest.Name)
                            .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }

                    script.UseWorkingDirectory(Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId)));
                    script.UseFile(path);
                    
                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                    {
                        using (var timeoutCancellationTokenSource = new CancellationTokenSource())
                        {
                            var task = script.ExecuteAsync<bool>();
                            
                            var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10), timeoutCancellationTokenSource.Token));
                            
                            if (completedTask == task) 
                            {
                                await timeoutCancellationTokenSource.CancelAsync();
                                return await task;
                            } else {
                                throw new TimeoutException("The operation has timed out.");
                            }
                        }
                    }
                    
                    result = await script.ExecuteAsync<bool>();

                    op.Complete();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Detect Install script");
        }

        return result;
    }

    public async Task<int> Redistributable_RunInstallScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
    {
        int result = default;

        var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
        var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);

        var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.Install);

        if (Path.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.Install))
        {
            logger?.LogTrace("Skipping install script; not supported on the current runtime");
            return result;
        }

        try
        {
            if (Path.Exists(path))
            {
                using (var op = logger.BeginOperation("Executing install detection script"))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.Install);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("RedistributableName", redistributableManifest.Name)
                            .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();

                    op.Complete();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Detect Install script");
        }

        return result;
    }

    public async Task<int> Redistributable_RunUninstallScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
    {
        int result = default;

        var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
        var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);

        var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.Uninstall);

        if (Path.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.Uninstall))
        {
            logger?.LogTrace("Skipping uninstall script; not supported on the current runtime");
            return result;
        }

        try
        {
            if (Path.Exists(path))
            {
                using (var op = logger.BeginOperation("Executing uninstall script"))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.Uninstall);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("RedistributableName", redistributableManifest.Name)
                            .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }

                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                        result = await script.ExecuteAsync<int>();

                    op.Complete();
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run an Uninstall script");
        }

        return result;
    }

    public async Task<int> Redistributable_RunBeforeStartScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
    {
        int result = default;

        try
        {
            var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
            
            var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.BeforeStart);

            if (File.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.BeforeStart))
            {
                logger?.LogTrace("Skipping before start script; not supported on the current runtime");
                return result;
            }

            using (var op = logger.BeginOperation("Executing before start script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.BeforeStart);
                    var playerAlias = await GameClient.GetPlayerAliasAsync(installDirectory, gameId);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("PlayerAlias", playerAlias);

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                            .Enrich("ScriptPath", path)
                            .Enrich("PlayerAlias", playerAlias)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("RedistributableName", redistributableManifest.Name)
                            .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

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

    public async Task<int> Redistributable_RunAfterStopScriptAsync(string installDirectory, Guid gameId, Guid redistributableId)
    {
        int result = default;

        try
        {
            var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
            
            var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.AfterStop);

            if (File.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.AfterStop))
            {
                logger?.LogTrace("Skipping after stop script; not supported on the current runtime");
                return result;
            }

            using (var op = logger.BeginOperation("Executing after stop script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.AfterStop);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("PlayerAlias", await GameClient.GetPlayerAliasAsync(installDirectory, gameId));

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("RedistributableManifestPath",
                                ManifestHelper.GetPath(installDirectory, redistributableId))
                            .Enrich("ScriptPath", path)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("RedistributableName", redistributableManifest.Name)
                            .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }
                    
                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

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

    public async Task<int> Redistributable_RunNameChangeScriptAsync(string installDirectory, Guid gameId, Guid redistributableId, string newName)
    {
        int result = default;

        try
        {
            var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);
            
            var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.NameChange);

            if (File.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.NameChange))
            {
                logger?.LogTrace("Skipping name change script; not supported on the current runtime");
                return result;
            }

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
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("OldPlayerAlias", oldName);
                    script.AddVariable("NewPlayerAlias", newName);

                    try
                    {
                        op.Enrich("InstallDirectory", installDirectory)
                          .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                          .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                          .Enrich("ScriptPath", path)
                          .Enrich("OldPlayerAlias", oldName)
                          .Enrich("NewPlayerAlias", newName)
                          .Enrich("GameTitle", gameManifest.Title)
                          .Enrich("GameId", gameManifest.Id)
                          .Enrich("RedistributableName", redistributableManifest.Name)
                          .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }
                    
                    var extractionPath = Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId), "Files");

                    script.UseWorkingDirectory(extractionPath);
                    script.UseFile(path);

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
    
    public async Task<bool> Redistributable_RunRunWrapperScriptAsync(string installDirectory, Guid gameId, Guid redistributableId, string executablePath, string arguments, string workingDirectory, CancellationToken cancellationToken = default)
    {
        bool result = false;

        try
        {
            var gameManifest = await ManifestHelper.ReadAsync<SDK.Models.Manifest.Game>(installDirectory, gameId);
            var redistributableManifest = await ManifestHelper.ReadAsync<Redistributable>(installDirectory, redistributableId);

            var path = ScriptHelper.GetScriptFilePath(installDirectory, redistributableId, Enums.ScriptType.RunWrapper);

            if (File.Exists(path) && !SupportsCurrentRuntime(redistributableManifest.Scripts, Enums.ScriptType.RunWrapper))
            {
                logger?.LogTrace("Skipping run wrapper script; not supported on the current runtime");
                return result;
            }

            using (var op = logger.BeginOperation("Executing run wrapper script"))
            {
                if (File.Exists(path))
                {
                    var script = powerShellScriptFactory.Create(Enums.ScriptType.RunWrapper);

                    script.AddVariable("InstallDirectory", installDirectory);
                    script.AddVariable("GameManifest", gameManifest);
                    script.AddVariable("RedistributableManifest", redistributableManifest);
                    script.AddVariable("DefaultInstallDirectory", settingsProvider.CurrentValue.Games.InstallDirectories.FirstOrDefault());
                    script.AddVariable("ServerAddress", connectionClient.GetServerAddress());
                    script.AddVariable("ExecutablePath", executablePath);
                    script.AddVariable("Arguments", arguments);
                    script.AddVariable("WorkingDirectory", workingDirectory);

                    try
                    {
                        op
                            .Enrich("InstallDirectory", installDirectory)
                            .Enrich("GameManifestPath", ManifestHelper.GetPath(installDirectory, gameId))
                            .Enrich("RedistributableManifestPath", ManifestHelper.GetPath(installDirectory, redistributableId))
                            .Enrich("ScriptPath", path)
                            .Enrich("ExecutablePath", executablePath)
                            .Enrich("GameTitle", gameManifest.Title)
                            .Enrich("GameId", gameManifest.Id)
                            .Enrich("RedistributableName", redistributableManifest.Name)
                            .Enrich("RedistributableId", redistributableManifest.Id);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogError(ex, "Could not enrich logs");
                    }

                    if (gameManifest.CustomFields != null && gameManifest.CustomFields.Any())
                    {
                        foreach (var customField in gameManifest.CustomFields)
                        {
                            script.AddVariable(customField.Name, customField.Value);
                        }
                    }

                    script.UseWorkingDirectory(Path.Combine(GameClient.GetMetadataDirectoryPath(installDirectory, redistributableId)));
                    script.UseFile(path);

                    if (Debug)
                        script.EnableDebug();

                    var handled = await RunScriptExternallyAsync(script);

                    if (!handled)
                    {
                        // Snapshot existing process IDs so we can identify child processes on stop
                        var existingPids = new HashSet<int>(Process.GetProcesses().Select(p => p.Id));

                        using (var registration = cancellationToken.Register(() =>
                        {
                            logger?.LogTrace("Stopping run wrapper script due to cancellation");
                            script.Stop();

                            // Kill any processes spawned during script execution
                            try
                            {
                                var currentProcesses = Process.GetProcesses();

                                foreach (var proc in currentProcesses)
                                {
                                    try
                                    {
                                        if (!existingPids.Contains(proc.Id) && !proc.HasExited)
                                        {
                                            logger?.LogTrace("Killing child process {ProcessId} ({ProcessName})", proc.Id, proc.ProcessName);
                                            proc.Kill(true);
                                        }
                                    }
                                    catch { }
                                    finally
                                    {
                                        proc.Dispose();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger?.LogWarning(ex, "Error killing child processes after run wrapper cancellation");
                            }
                        }))
                        {
                            await script.ExecuteAsync<int>();
                        }
                    }

                    result = true;
                }
                else
                {
                    logger?.LogTrace("No run wrapper script found");
                }

                op.Complete();
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Ran into an unexpected error when attempting to run a Run Wrapper script");
        }

        return result;
    }

    public async Task<Package> Redistributable_RunPackageScriptAsync(Script packageScript, Redistributable redistributable, string latestArchivePath = null)
    {
        try
        {
            using (var op = logger.BeginOperation("Executing redistributable package script"))
            {
                var script = powerShellScriptFactory.Create(Enums.ScriptType.Package);

                script.AddVariable("Redistributable", redistributable);

                if (!string.IsNullOrEmpty(latestArchivePath))
                    script.AddVariable("LatestArchivePath", latestArchivePath);

                script.UseInline(packageScript.Contents);
                
                try
                {
                    op
                        .Enrich("RedistributableId", redistributable.Id)
                        .Enrich("RedistributableName", redistributable.Name)
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
            logger?.LogError(ex, "Could not execute redistributable package script");
        }

        return null;
    }
}